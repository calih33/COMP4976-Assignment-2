"""End-to-end checks for MEMS.Api through the gateway (AppHost must be running).

Run from the repo root:  python3 -I tools/api_smoke_test.py [samples-output.json]
Creates and then deletes its own test position, candidate and municipality.
"""
import json
import struct
import sys
import urllib.error
import urllib.request
import uuid
import zlib

BASE = "http://localhost:5001"
results = []
samples = {}


def call(method, path, body=None, token=None, raw=None, content_type=None):
    headers = {"Accept": "application/json"}
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if raw is not None:
        data = raw
        headers["Content-Type"] = content_type
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req) as r:
            payload = r.read()
            return r.status, r.headers.get("Content-Type", ""), payload
    except urllib.error.HTTPError as e:
        return e.code, e.headers.get("Content-Type", ""), e.read()


def js(payload):
    try:
        return json.loads(payload)
    except Exception:
        return None


def check(name, ok, detail=""):
    results.append((name, ok))
    print(f"{'PASS' if ok else 'FAIL'}  {name}" + (f"  -> {detail}" if not ok and detail else ""))


def multipart(field, filename, content_type, data):
    boundary = uuid.uuid4().hex
    body = (
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"{field}\"; filename=\"{filename}\"\r\n"
        f"Content-Type: {content_type}\r\n\r\n"
    ).encode() + data + f"\r\n--{boundary}--\r\n".encode()
    return body, f"multipart/form-data; boundary={boundary}"


def tiny_png():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    raw = b"\x00\xff\x00\x00"  # one red pixel
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


# ---------- public reads ----------
s, _, p = call("GET", "/api/municipalities")
munis = js(p) or []
check("GET /api/municipalities lists 4 tenants", s == 200 and len(munis) == 4, f"{s} {len(munis)}")
samples["GET /api/municipalities"] = munis[:1]
ids = {m["slug"]: m["id"] for m in munis}

s, _, p = call("GET", "/api/municipalities/Richmond")
check("GET /api/municipalities/Richmond (case-insensitive slug)", s == 200 and js(p)["slug"] == "richmond", s)

s, _, p = call("GET", "/api/burnaby/candidates")
bc = js(p) or []
check("GET /api/burnaby/candidates returns only Burnaby (9)",
      s == 200 and len(bc) == 9 and all(c["municipalityId"] == ids["burnaby"] for c in bc), f"{s} {len(bc)}")
samples["GET /api/burnaby/candidates"] = bc[:1]

s, _, p = call("GET", "/api/burnaby/candidates?search=WONG")
check("search is case-insensitive (WONG -> Ethan Wong)", s == 200 and [c["name"] for c in js(p)] == ["Ethan Wong"], p[:120])

s, _, p = call("GET", "/api/burnaby/positions")
bpos = js(p) or []
check("GET /api/burnaby/positions returns 3", s == 200 and len(bpos) == 3, s)
samples["GET /api/burnaby/positions"] = bpos[:1]
mayor = next(x for x in bpos if x["name"] == "Mayor")
s, _, p = call("GET", f"/api/burnaby/candidates?positionId={mayor['id']}")
check("filter by position (Burnaby Mayor -> 2)", s == 200 and len(js(p)) == 2, p[:120])

s, ct, p = call("GET", "/api/nowhere/candidates")
check("unknown tenant -> 404 ProblemDetails", s == 404 and "problem+json" in ct, f"{s} {ct}")
samples["404 unknown tenant"] = js(p)

surrey_c = js(call("GET", "/api/surrey/candidates")[2])
surrey_pos = js(call("GET", "/api/surrey/positions")[2])
s, _, p = call("GET", f"/api/burnaby/candidates/{surrey_c[0]['id']}")
check("Surrey candidate id via /api/burnaby -> 404", s == 404, s)

# ---------- auth ----------
s, ct, p = call("POST", "/api/burnaby/positions", {"name": "X", "seatsAvailable": 1})
check("write without token -> 401 ProblemDetails", s == 401 and "problem+json" in ct, f"{s} {ct}")
samples["401 no token"] = js(p)

s, _, p = call("POST", "/api/auth/login", {"userName": "burnabyadmin", "password": "wrong"})
check("login wrong password -> 401", s == 401, s)
samples["401 bad login"] = js(p)

s, _, p = call("POST", "/api/auth/login", {"userName": "", "password": ""})
check("login empty body -> 400 validation", s == 400 and "errors" in (js(p) or {}), s)

s, _, p = call("POST", "/api/auth/login", {"userName": "burnabyadmin", "password": "P@$$w0rd"})
login = js(p) or {}
check("login burnabyadmin -> token, tenant burnaby", s == 200 and login.get("tenant") == "burnaby"
      and login.get("role") == "Municipality Admin", p[:200])
b = login.get("token")
samples["POST /api/auth/login"] = {**login, "token": login.get("token", "")[:24] + "..."}

s, _, p = call("POST", "/api/burnaby/positions", {"name": "X", "seatsAvailable": 1}, token="not-a-jwt")
check("write with invalid token -> 401", s == 401, s)
s, _, p = call("GET", "/api/burnaby/candidates", token="not-a-jwt")
check("public GET with invalid token -> treated as anonymous (200)", s == 200, s)

# ---------- cross-tenant attacks (spec examples) ----------
s, _, p = call("GET", "/api/surrey/candidates", token=b)
check("burnabyadmin GET /api/surrey/candidates -> 403", s == 403, s)
samples["403 cross-tenant"] = js(p)
s, _, p = call("PUT", f"/api/surrey/candidates/{surrey_c[0]['id']}",
               {"name": "Hacked", "profile": "x", "positionId": surrey_c[0]["positionId"]}, token=b)
check("burnabyadmin PUT /api/surrey/candidates/{id} -> 403", s == 403, s)
s, _, p = call("PUT", f"/api/burnaby/candidates/{surrey_c[0]['id']}",
               {"name": "Hacked", "profile": "x", "positionId": surrey_c[0]["positionId"]}, token=b)
check("burnabyadmin PUT /api/burnaby/candidates/{surreyId} -> 404", s == 404, s)
s, _, p = call("POST", "/api/burnaby/candidates",
               {"name": "Sneaky", "profile": "x", "positionId": surrey_pos[0]["id"]}, token=b)
check("burnabyadmin creates candidate with a Surrey position -> 400", s == 400 and "PositionId" in json.dumps(js(p)), p[:200])
s, _, p = call("DELETE", f"/api/burnaby/positions/{surrey_pos[0]['id']}", token=b)
check("burnabyadmin DELETE Surrey position via /api/burnaby -> 404", s == 404, s)
check("Surrey candidate unchanged", js(call("GET", f"/api/surrey/candidates/{surrey_c[0]['id']}")[2])["name"] == surrey_c[0]["name"])

# ---------- CRUD as burnabyadmin ----------
s, _, p = call("POST", "/api/burnaby/positions", {"name": "Test Position", "seatsAvailable": 0}, token=b)
check("position with 0 seats -> 400", s == 400, s)
samples["400 validation"] = js(p)
s, _, p = call("POST", "/api/burnaby/positions", {"name": "Test Position", "seatsAvailable": 3}, token=b)
pos = js(p) or {}
check("POST position -> 201 in Burnaby", s == 201 and pos.get("municipalityId") == ids["burnaby"], p[:200])
samples["POST /api/burnaby/positions"] = pos
s, _, p = call("POST", "/api/burnaby/positions", {"name": "Test Position", "seatsAvailable": 3}, token=b)
check("duplicate position name -> 400", s == 400, s)

s, _, p = call("POST", "/api/burnaby/candidates",
               {"name": "Alex Rivera", "profile": "Test candidate.", "email": "not-an-email", "positionId": pos["id"]}, token=b)
check("candidate with bad email -> 400", s == 400 and "Email" in json.dumps(js(p)), p[:200])
s, _, p = call("POST", "/api/burnaby/candidates",
               {"name": "Alex Rivera", "profile": "Test candidate.", "website": "https://example.com/alex",
                "email": "alex@example.com", "positionId": pos["id"]}, token=b)
cand = js(p) or {}
check("POST candidate -> 201 with position name", s == 201 and cand.get("positionName") == "Test Position", p[:200])
samples["POST /api/burnaby/candidates"] = cand

s, _, p = call("PUT", f"/api/burnaby/candidates/{cand['id']}",
               {"name": "Alex Rivera-Jones", "profile": "Updated.", "positionId": pos["id"]}, token=b)
check("PUT candidate -> 200 updated", s == 200 and js(p)["name"] == "Alex Rivera-Jones", p[:200])

body, ct = multipart("file", "fake.png", "image/png", b"this is not an image")
s, _, p = call("PUT", f"/api/burnaby/candidates/{cand['id']}/photo", raw=body, content_type=ct, token=b)
check("upload text file renamed .png -> 400", s == 400 and "not a valid" in json.dumps(js(p)), p[:200])
body, ct = multipart("file", "photo.gif", "image/gif", b"GIF89a....")
s, _, p = call("PUT", f"/api/burnaby/candidates/{cand['id']}/photo", raw=body, content_type=ct, token=b)
check("upload .gif -> 400", s == 400, s)
body, ct = multipart("file", "big.jpg", "image/jpeg", b"\xff\xd8\xff" + b"0" * (2 * 1024 * 1024 + 10))
s, _, p = call("PUT", f"/api/burnaby/candidates/{cand['id']}/photo", raw=body, content_type=ct, token=b)
check("upload >2 MB -> 400", s == 400 and "2 MB" in json.dumps(js(p)), p[:200])
body, ct = multipart("file", "photo.png", "image/png", tiny_png())
s, _, p = call("PUT", f"/api/burnaby/candidates/{cand['id']}/photo", raw=body, content_type=ct, token=b)
photo_url = (js(p) or {}).get("photoUrl")
check("upload real PNG -> 200 with photoUrl", s == 200 and photo_url, p[:200])
s, ctype, p = call("GET", photo_url)
check("GET photoUrl -> image/png", s == 200 and ctype.startswith("image/png"), f"{s} {ctype}")
s, _, _ = call("GET", photo_url.replace("/api/burnaby/", "/api/surrey/"))
check("same image via /api/surrey -> 404", s == 404, s)

s, _, p = call("DELETE", f"/api/burnaby/candidates/{cand['id']}", token=b)
check("DELETE candidate -> 204", s == 204, s)
s, _, _ = call("GET", photo_url)
check("photo gone after candidate delete -> 404", s == 404, s)
s, _, p = call("DELETE", f"/api/burnaby/positions/{pos['id']}", token=b)
check("DELETE position -> 204", s == 204, s)

# ---------- municipality management ----------
s, _, p = call("POST", "/api/municipalities", {"name": "Nope", "slug": "nope", "electionDate": "2026-10-17", "themeColor": "#123456"}, token=b)
check("burnabyadmin POST /api/municipalities -> 403", s == 403, s)
burnaby = js(call("GET", "/api/municipalities/burnaby")[2])
s, _, p = call("PUT", f"/api/municipalities/{ids['surrey']}", {**burnaby, "name": "Surrey"}, token=b)
check("burnabyadmin PUT Surrey municipality -> 403", s == 403, s)
s, _, p = call("PUT", f"/api/municipalities/{ids['burnaby']}", {**burnaby, "slug": "burnaby-city"}, token=b)
check("burnabyadmin changing own slug -> 400", s == 400 and "Slug" in json.dumps(js(p)), p[:200])
s, _, p = call("PUT", f"/api/municipalities/{ids['burnaby']}", burnaby, token=b)
check("burnabyadmin PUT own municipality -> 200", s == 200, p[:200])

sup = js(call("POST", "/api/auth/login", {"userName": "super", "password": "P@$$w0rd"})[2])["token"]
s, _, p = call("GET", "/api/surrey/candidates", token=sup)
check("super GET /api/surrey/candidates -> 200", s == 200 and len(js(p)) == 10, s)
s, _, p = call("POST", "/api/municipalities", {"name": "Test Town", "slug": "Bad Slug", "electionDate": "2026-10-17", "themeColor": "#123456"}, token=sup)
check("super create with bad slug -> 400", s == 400, s)
s, _, p = call("POST", "/api/municipalities", {"name": "Burnaby", "slug": "burnaby", "electionDate": "2026-10-17", "themeColor": "#123456"}, token=sup)
check("super create duplicate name/slug -> 400", s == 400 and "Slug" in json.dumps(js(p)), p[:200])
s, _, p = call("POST", "/api/municipalities", {"name": "Test Town", "slug": "test-town", "province": "British Columbia",
                                               "electionDate": "2026-10-17", "description": "Temporary", "themeColor": "#123456"}, token=sup)
town = js(p) or {}
check("super POST /api/municipalities -> 201", s == 201 and town.get("slug") == "test-town", p[:200])
samples["POST /api/municipalities"] = town
s, _, p = call("POST", "/api/test-town/positions", {"name": "Mayor", "seatsAvailable": 1}, token=sup)
check("super creates position in new tenant -> 201", s == 201, s)
s, _, p = call("GET", "/api/test-town/positions")
check("new tenant works without new apps (public GET)", s == 200 and len(js(p)) == 1, s)
s, _, p = call("DELETE", f"/api/municipalities/{town['id']}", token=sup)
check("super DELETE municipality -> 204", s == 204, s)
s, _, p = call("GET", "/api/test-town/positions")
check("deleted tenant -> 404", s == 404, s)

# ---------- docs ----------
s, _, p = call("GET", "/api/openapi/v1.json")
doc = js(p) or {}
check("OpenAPI document has tenant paths + Bearer scheme",
      s == 200 and "/api/{tenant}/candidates" in doc.get("paths", {}) and "Bearer" in doc.get("components", {}).get("securitySchemes", {}), s)
s, _, _ = call("GET", "/api/swagger/index.html")
check("Swagger UI served at /api/swagger", s == 200, s)

with open(sys.argv[1] if len(sys.argv) > 1 else "/dev/null", "w") as f:
    json.dump(samples, f, indent=2)

failed = [n for n, ok in results if not ok]
print(f"\n{len(results) - len(failed)}/{len(results)} passed")
sys.exit(1 if failed else 0)

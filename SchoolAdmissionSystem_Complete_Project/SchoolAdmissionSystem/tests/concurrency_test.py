"""
Concurrent seat-allocation check. Start stack first, then:
  python tests/concurrency_test.py
Expected: exactly capacity requests succeed; remaining requests receive 409.
"""
import concurrent.futures
import json
import urllib.error
import urllib.request
import uuid

BASE = "http://localhost:8080"

def call(path, method="GET", payload=None, student=None):
    headers = {"Content-Type": "application/json"}
    if student:
        headers["X-Student-Id"] = student
    body = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(BASE + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code

capacity = 5
requests_count = 30
_, = (None,)
name = "Concurrent " + uuid.uuid4().hex[:8]
req = urllib.request.Request(BASE + "/classes",
    data=json.dumps({"name": name, "totalSeats": capacity}).encode(),
    headers={"Content-Type": "application/json"}, method="POST")
with urllib.request.urlopen(req) as response:
    class_id = json.loads(response.read())["id"]

def reserve(i):
    student = str(uuid.uuid4().int)[:17]
    return call(f"/classes/{class_id}/holds", "POST", student=student)

with concurrent.futures.ThreadPoolExecutor(max_workers=requests_count) as pool:
    results = list(pool.map(reserve, range(requests_count)))

successes = results.count(201)
conflicts = results.count(409)
print(f"Success: {successes}; capacity conflicts: {conflicts}; other: {len(results)-successes-conflicts}")
if successes != capacity or conflicts != requests_count-capacity:
    raise SystemExit("FAIL: concurrency/capacity result did not match expected values")
print("PASS: capacity was not exceeded.")

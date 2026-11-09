"""
Standard-library API checks. Start the stack first:
  docker compose up --build
  python tests/api_smoke_test.py
"""
import json
import urllib.error
import urllib.request
import unittest
import uuid

BASE = "http://localhost:8080"

def request(path, method="GET", payload=None, student=None):
    headers = {"Content-Type": "application/json"}
    if student:
        headers["X-Student-Id"] = student
    data = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=10) as res:
            body = res.read()
            return res.status, json.loads(body) if body else None
    except urllib.error.HTTPError as e:
        body = e.read()
        return e.code, json.loads(body) if body else None

class AdmissionApiTests(unittest.TestCase):
    def test_1_create_and_list_class(self):
        name = "Automated Test " + uuid.uuid4().hex[:8]
        status, created = request("/classes", "POST", {"name": name, "totalSeats": 2})
        self.assertEqual(status, 201)
        self.assertTrue(created["id"].startswith("c_"))
        status, rows = request("/classes")
        self.assertEqual(status, 200)
        self.assertTrue(any(x["id"] == created["id"] for x in rows))

    def test_2_hold_confirm_and_idempotent_confirm(self):
        name = "Confirm Test " + uuid.uuid4().hex[:8]
        _, cls = request("/classes", "POST", {"name": name, "totalSeats": 1})
        student = str(uuid.uuid4().int)[:17]
        status, hold = request(f"/classes/{cls['id']}/holds", "POST", student=student)
        self.assertEqual(status, 201)
        status, confirmed = request(f"/holds/{hold['holdId']}/confirm", "POST", student=student)
        self.assertEqual(status, 200)
        self.assertEqual(confirmed["status"], "CONFIRMED")
        status, again = request(f"/holds/{hold['holdId']}/confirm", "POST", student=student)
        self.assertEqual(status, 200)
        self.assertEqual(again["status"], "CONFIRMED")

    def test_3_capacity_never_overbooks(self):
        name = "Capacity Test " + uuid.uuid4().hex[:8]
        _, cls = request("/classes", "POST", {"name": name, "totalSeats": 1})
        student = str(uuid.uuid4().int)[:17]
        status, _ = request(f"/classes/{cls['id']}/holds", "POST", student=student)
        self.assertEqual(status, 201)
        other_student = str(uuid.uuid4().int)[:17]
        status, _ = request(f"/classes/{cls['id']}/holds", "POST", student=other_student)
        self.assertEqual(status, 409)
        _, detail = request(f"/classes/{cls['id']}")
        self.assertLessEqual(detail["heldSeats"] + detail["confirmedSeats"], detail["totalSeats"])

if __name__ == "__main__":
    unittest.main(verbosity=2)

import { useEffect, useState } from "react";

const API = import.meta.env.VITE_API_URL || "http://localhost:8080";
type SchoolClass = { id:string; name:string; totalSeats:number; heldSeats:number; confirmedSeats:number; availableSeats:number };
type Hold = { holdId:string; classId:string; className?:string; status:string; expiresAt:string };
const studentKey = "school-admission-student-id";

async function call(path:string, options:RequestInit = {}) {
  const studentId = localStorage.getItem(studentKey) || "";
  const headers = { "Content-Type":"application/json", ...(studentId ? {"X-Student-Id":studentId}:{}), ...(options.headers || {}) };
  const response = await fetch(`${API}${path}`, {...options, headers});
  if (response.status === 204) return null;
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || `Request failed (${response.status})`);
  return data;
}

export default function App() {
  const [tab,setTab] = useState<"parent"|"admin">("parent");
  const [studentId,setStudentId] = useState(localStorage.getItem(studentKey) || "");
  const [classes,setClasses] = useState<SchoolClass[]>([]);
  const [holds,setHolds] = useState<Hold[]>([]);
  const [name,setName] = useState("");
  const [seats,setSeats] = useState("40");
  const [error,setError] = useState("");
  const [message,setMessage] = useState("");
  const [busy,setBusy] = useState(false);

  async function refresh() {
    try {
      const list = await call("/classes");
      setClasses(list);
      if (studentId.length === 17) setHolds(await call("/me/holds"));
    } catch(e) { setError((e as Error).message); }
  }
  useEffect(() => { refresh(); const timer=setInterval(refresh,5000); return ()=>clearInterval(timer); }, [studentId]);
  function saveStudent() {
    if (!/^\d{17}$/.test(studentId)) { setError("Please enter a valid 17-digit birth registration number."); return; }
    localStorage.setItem(studentKey,studentId); setMessage("Student ID saved on this device."); setError("");
  }
  async function action(fn:()=>Promise<unknown>) {
    setBusy(true); setError(""); setMessage("");
    try { await fn(); await refresh(); } catch(e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <main>
    <header><div><div className="eyebrow">ONLINE ADMISSION PORTAL</div><h1>School Admission System</h1><p>Fair, first-come-first-served seat reservation</p></div><span className="live">● LIVE SEAT STATUS</span></header>
    <nav><button className={tab==="parent"?"active":""} onClick={()=>setTab("parent")}>Parent / Student</button><button className={tab==="admin"?"active":""} onClick={()=>setTab("admin")}>School Admin</button></nav>
    {error && <div className="alert error">{error}</div>}{message && <div className="alert success">{message}</div>}
    {tab==="parent" ? <section>
      <div className="panel"><h2>Student identification</h2><p>Enter the student's 17-digit birth registration number. It is saved in this browser so you can return to your application.</p>
        <div className="inline"><input value={studentId} maxLength={17} onChange={e=>setStudentId(e.target.value.replace(/\D/g,""))} placeholder="17-digit birth registration number"/><button onClick={saveStudent}>Save ID</button></div>
      </div>
      <h2>Available classes</h2><div className="cards">{classes.map(c=><article className="class-card" key={c.id}>
        <div className="card-top"><h3>{c.name}</h3><span className={c.availableSeats>0?"pill":"pill full"}>{c.availableSeats} seats left</span></div>
        <div className="stats"><div><b>{c.totalSeats}</b><small>Total</small></div><div><b>{c.heldSeats}</b><small>On hold</small></div><div><b>{c.confirmedSeats}</b><small>Confirmed</small></div></div>
        <button disabled={busy || c.availableSeats===0 || !/^\d{17}$/.test(studentId)} onClick={()=>action(async()=>{const h=await call(`/classes/${c.id}/holds`,{method:"POST"});setMessage(`Seat reserved until ${new Date(h.expiresAt).toLocaleString()}.`);})}>Apply / Reserve seat</button>
      </article>)}</div>
      <h2>My applications</h2>{holds.length===0?<p className="muted">No applications found for this student ID.</p>:<div className="table-wrap"><table><thead><tr><th>Class</th><th>Status</th><th>Reservation expires</th><th>Action</th></tr></thead><tbody>{holds.map(h=><tr key={h.holdId}><td>{h.className || h.classId}</td><td><span className="status">{h.status}</span></td><td>{new Date(h.expiresAt).toLocaleString()}</td><td>{h.status==="HELD" && <div className="actions"><button onClick={()=>action(async()=>{await call(`/holds/${h.holdId}/confirm`,{method:"POST"});setMessage("Admission confirmed. (Demo payment successful.)");})}>Pay & Confirm</button><button className="secondary" onClick={()=>action(async()=>{await call(`/holds/${h.holdId}`,{method:"DELETE"});setMessage("Reservation cancelled.");})}>Cancel</button></div>}</td></tr>)}</tbody></table></div>}
    </section> : <section>
      <div className="panel"><h2>Create a class / shift</h2><p>Each class and shift has its own seat capacity.</p><form onSubmit={e=>{e.preventDefault();action(async()=>{await call("/classes",{method:"POST",body:JSON.stringify({name,totalSeats:Number(seats)})});setName("");setMessage("Class created successfully.");});}}>
        <label>Class and shift name<input required value={name} onChange={e=>setName(e.target.value)} placeholder="Class 6 - Morning Shift"/></label>
        <label>Total seats<input required type="number" min="1" value={seats} onChange={e=>setSeats(e.target.value)}/></label><button disabled={busy}>Create class</button>
      </form></div>
      <h2>Admission overview</h2><div className="table-wrap"><table><thead><tr><th>Class / Shift</th><th>Total</th><th>Held</th><th>Confirmed</th><th>Available</th></tr></thead><tbody>{classes.map(c=><tr key={c.id}><td>{c.name}</td><td>{c.totalSeats}</td><td>{c.heldSeats}</td><td>{c.confirmedSeats}</td><td><b>{c.availableSeats}</b></td></tr>)}</tbody></table></div>
      <p className="muted">Admin access is open in this take-home demo because the assignment does not specify an authentication system. Add real authentication before production use.</p>
    </section>}
    <footer>School Admission System · Seat counts refresh automatically every 5 seconds</footer>
  </main>;
}

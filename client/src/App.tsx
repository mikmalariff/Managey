import { useEffect, useState } from 'react'
import { Link, Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { api } from './api'
import { useAuth, useMe } from './auth'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Attendance from './pages/Attendance'
import Leave from './pages/Leave'
import Requests from './pages/Requests'
import NewRequest from './pages/NewRequest'
import RequestView from './pages/RequestView'
import Approvals from './pages/Approvals'
import People from './pages/People'
import PersonEdit from './pages/PersonEdit'
import Documents from './pages/Documents'
import Setup from './pages/Setup'
import Audit from './pages/Audit'
import Account from './pages/Account'

export default function App() {
  const { me, loading } = useAuth()
  if (loading) return null
  if (!me) return <Login />
  return <Shell />
}

function Shell() {
  const { me, isAdmin, isHR, isManager } = useMe()
  const { logout } = useAuth()
  const location = useLocation()
  const [toApprove, setToApprove] = useState(0)

  // Refresh the approvals badge whenever the page changes.
  useEffect(() => {
    api.get<unknown[]>('/api/requests?scope=pending').then(r => setToApprove(r.length)).catch(() => {})
  }, [location.pathname])

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <strong>Managey</strong>
          <span>{me.companyName}</span>
        </div>

        <nav className="nav-group">
          <NavLink to="/" end>Overview</NavLink>
          <NavLink to="/attendance">Attendance</NavLink>
          <NavLink to="/leave">Leave</NavLink>
          <NavLink to="/requests">My requests</NavLink>
          <NavLink to="/approvals">
            Approvals {toApprove > 0 && <span className="count">{toApprove}</span>}
          </NavLink>
        </nav>

        <nav className="nav-group">
          <div className="nav-group-title">Company</div>
          <NavLink to="/people">People</NavLink>
          <NavLink to="/documents">Documents</NavLink>
        </nav>

        {(isHR || isManager) && (
          <nav className="nav-group">
            <div className="nav-group-title">Manage</div>
            {isHR && <NavLink to="/requests/all">All requests</NavLink>}
            <NavLink to="/attendance/team">{isHR ? 'Attendance report' : 'Team attendance'}</NavLink>
            {isHR && <NavLink to="/setup">Setup</NavLink>}
            {isAdmin && <NavLink to="/audit">Audit log</NavLink>}
          </nav>
        )}

        <div className="sidebar-foot">
          <div className="name">{me.fullName}</div>
          <div className="muted">{me.position || me.role}</div>
          <div className="links">
            <Link to="/account">Account</Link>
            <button className="link" onClick={logout}>Sign out</button>
          </div>
        </div>
      </aside>

      <main className="main">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/attendance" element={<Attendance />} />
          <Route path="/attendance/team" element={isManager ? <Attendance team /> : <Navigate to="/attendance" />} />
          <Route path="/leave" element={<Leave />} />
          <Route path="/requests" element={<Requests />} />
          <Route path="/requests/all" element={isHR ? <Requests all /> : <Navigate to="/requests" />} />
          <Route path="/requests/new" element={<NewRequest />} />
          <Route path="/requests/:id" element={<RequestView />} />
          <Route path="/approvals" element={<Approvals />} />
          <Route path="/people" element={<People />} />
          <Route path="/people/new" element={isHR ? <PersonEdit /> : <Navigate to="/people" />} />
          <Route path="/people/:id" element={<PersonEdit />} />
          <Route path="/documents" element={<Documents />} />
          <Route path="/setup" element={isHR ? <Setup /> : <Navigate to="/" />} />
          <Route path="/audit" element={isAdmin ? <Audit /> : <Navigate to="/" />} />
          <Route path="/account" element={<Account />} />
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </main>
    </div>
  )
}

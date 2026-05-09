import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { 
  LayoutDashboard, 
  FileText, 
  CreditCard, 
  BarChart3, 
  Plus, 
  DollarSign, 
  CheckCircle2, 
  Clock,
  LogOut,
  Workflow,
  Users,
  ShieldCheck,
  Activity,
  ChevronRight,
  ShieldAlert,
  Search,
  Filter,
  RefreshCcw,
  UserPlus
} from 'lucide-react';
import { Toaster, toast } from 'react-hot-toast';

const GATEWAY_URL = 'http://localhost:5001';

const OrchestrationFlow = ({ api }) => {
  const [flows, setFlows] = useState([]);
  const [selectedFlow, setSelectedFlow] = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchFlows();
  }, []);

  const fetchFlows = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/orchestration/flows');
      setFlows(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải dữ liệu Orchestration');
    } finally {
      setLoading(false);
    }
  };

  const getStatusColor = (state) => {
    switch (state) {
      case 'InvoiceFinalized': return '#22c55e';
      case 'PaymentFailed': return '#ef4444';
      case 'CompensationCompleted': return '#eab308';
      default: return '#38bdf8';
    }
  };

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <h2 style={{ fontSize: '1.25rem' }}>Luồng điều phối (Saga Flows)</h2>
        <button className="btn btn-outline" onClick={fetchFlows} disabled={loading}>
          <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Làm mới
        </button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: selectedFlow ? '1fr 1fr' : '1fr', gap: '2rem' }}>
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr>
                <th>Hóa đơn</th>
                <th>Loại</th>
                <th>Trạng thái</th>
                <th>Cập nhật</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {flows.map(flow => (
                <tr key={flow.id} 
                    style={{ cursor: 'pointer', background: selectedFlow?.id === flow.id ? 'rgba(56, 189, 248, 0.05)' : 'transparent' }}
                    onClick={() => setSelectedFlow(flow)}>
                  <td style={{ fontSize: '0.75rem', opacity: 0.6 }}>{flow.invoiceId.substring(0, 8)}...</td>
                  <td>{flow.flowType}</td>
                  <td>
                    <span className="status-badge" style={{ background: `${getStatusColor(flow.currentState)}20`, color: getStatusColor(flow.currentState) }}>
                      {flow.currentState}
                    </span>
                  </td>
                  <td style={{ fontSize: '0.875rem' }}>{new Date(flow.updatedAtUtc).toLocaleTimeString()}</td>
                  <td><ChevronRight size={16} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {selectedFlow && (
          <div style={{ borderLeft: '1px solid rgba(255, 255, 255, 0.1)', paddingLeft: '2rem' }}>
            <h3 style={{ marginBottom: '1.5rem', color: '#94a3b8' }}>Chi tiết sự kiện: {selectedFlow.invoiceId.substring(0, 8)}</h3>
            <div className="timeline">
              {selectedFlow.steps.map((step, idx) => (
                <div key={step.id} className="timeline-item">
                  <div className="timeline-icon">
                    {step.stepType.includes('Failed') ? <ShieldAlert size={20} color="#ef4444" /> : <Activity size={20} color="#38bdf8" />}
                  </div>
                  <div className="timeline-content">
                    <div className="timeline-header">
                      <span className="timeline-title">{step.stepType}</span>
                      <span className="timeline-time">{new Date(step.occurredAtUtc).toLocaleTimeString()}</span>
                    </div>
                    {step.payloadJson && (
                      <pre className="timeline-payload">
                        {JSON.stringify(JSON.parse(step.payloadJson), null, 2)}
                      </pre>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

const IdentityManager = ({ api }) => {
  const [activeSubTab, setActiveSubTab] = useState('users');
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [loading, setLoading] = useState(false);

  // Modal states
  const [showRoleModal, setShowRoleModal] = useState(false);
  const [showPermissionModal, setShowPermissionModal] = useState(false);
  const [selectedRole, setSelectedRole] = useState(null);
  const [allPermissions, setAllPermissions] = useState([]);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState([]);
  const [showUserModal, setShowUserModal] = useState(false);
  const [userForm, setUserForm] = useState({ username: '', email: '', password: '', roleNames: [] });

  useEffect(() => {
    if (activeSubTab === 'users') {
      fetchUsers();
      if (roles.length === 0) fetchRoles();
    } else {
      fetchRoles();
    }
  }, [activeSubTab]);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/users');
      setUsers(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách người dùng');
    } finally {
      setLoading(false);
    }
  };

  const fetchRoles = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/roles');
      setRoles(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách vai trò');
    } finally {
      setLoading(false);
    }
  };

  const fetchAllPermissions = async () => {
    try {
      const res = await api.get('/api/v1/roles/permissions');
      setAllPermissions(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách quyền');
    }
  };

  const handleCreateRole = async (e) => {
    e.preventDefault();
    try {
      await api.post('/api/v1/roles', roleForm);
      toast.success('Tạo vai trò thành công');
      setShowRoleModal(false);
      setRoleForm({ name: '', description: '' });
      fetchRoles();
    } catch (error) {
      toast.error(error.response?.data?.message || 'Lỗi khi tạo vai trò');
    }
  };

  const handleCreateUser = async (e) => {
    e.preventDefault();
    try {
      await api.post('/api/v1/users', userForm);
      toast.success('Thêm người dùng thành công');
      setShowUserModal(false);
      setUserForm({ username: '', email: '', password: '', roleNames: [] });
      fetchUsers();
    } catch (error) {
      toast.error(error.response?.data?.message || 'Lỗi khi thêm người dùng');
    }
  };

  const toggleUserRole = (roleName) => {
    setUserForm(prev => ({
      ...prev,
      roleNames: prev.roleNames.includes(roleName) 
        ? prev.roleNames.filter(r => r !== roleName) 
        : [...prev.roleNames, roleName]
    }));
  };

  const openPermissionModal = async (role) => {
    setSelectedRole(role);
    setSelectedPermissionIds(role.permissions?.map(p => p.id) || []);
    if (allPermissions.length === 0) await fetchAllPermissions();
    setShowPermissionModal(true);
  };

  const handleSavePermissions = async () => {
    try {
      await api.put(`/api/v1/roles/${selectedRole.id}/permissions`, {
        permissionIds: selectedPermissionIds
      });
      toast.success('Cập nhật quyền thành công');
      setShowPermissionModal(false);
      fetchRoles();
    } catch (error) {
      toast.error('Lỗi khi cập nhật quyền');
    }
  };

  const togglePermission = (id) => {
    setSelectedPermissionIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  return (
    <div className="card">
      <div className="tab-header">
        <div className={`tab-btn ${activeSubTab === 'users' ? 'active' : ''}`} onClick={() => setActiveSubTab('users')}>
          <Users size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Người dùng
        </div>
        <div className={`tab-btn ${activeSubTab === 'roles' ? 'active' : ''}`} onClick={() => setActiveSubTab('roles')}>
          <ShieldCheck size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Vai trò & Quyền
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
        <button className="btn btn-primary" onClick={() => activeSubTab === 'roles' ? setShowRoleModal(true) : setShowUserModal(true)}>
          <Plus size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Thêm {activeSubTab === 'users' ? 'Người dùng' : 'Vai trò'}
        </button>
      </div>

      <div className="table-wrapper">
        {activeSubTab === 'users' ? (
          <table className="table">
            <thead>
              <tr>
                <th>Username</th>
                <th>Email</th>
                <th>Vai trò</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {users.map(user => (
                <tr key={user.id}>
                  <td>{user.username}</td>
                  <td>{user.email}</td>
                  <td>
                    {user.roles?.map(r => (
                      <span key={r} className="status-badge" style={{ background: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8', marginRight: '4px' }}>
                        {r}
                      </span>
                    ))}
                  </td>
                  <td>
                    <span className={`status-badge ${user.isActive ? 'status-paid' : 'status-pending'}`}>
                      {user.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td><button className="btn btn-outline" style={{ padding: '4px 8px' }}>Sửa</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Tên vai trò</th>
                <th>Mô tả</th>
                <th>Quyền</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {roles.map(role => (
                <tr key={role.id}>
                  <td style={{ fontWeight: 600 }}>{role.name} {role.isSystem && <span style={{ fontSize: '0.6rem', background: '#334155', padding: '2px 4px', borderRadius: '4px', verticalAlign: 'middle' }}>SYSTEM</span>}</td>
                  <td style={{ color: '#94a3b8', fontSize: '0.875rem' }}>{role.description || 'N/A'}</td>
                  <td>
                    <span className="status-badge" style={{ background: 'rgba(255, 255, 255, 0.05)', color: '#94a3b8' }}>
                      {role.permissions?.length || 0} permissions
                    </span>
                  </td>
                  <td>
                    <button 
                      className="btn btn-outline" 
                      style={{ padding: '4px 8px' }}
                      onClick={() => openPermissionModal(role)}
                    >
                      Quản lý quyền
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* User Creation Modal */}
      {showUserModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Thêm người dùng mới</h2>
            <form onSubmit={handleCreateUser}>
              <div className="form-group">
                <label className="form-label">Tên đăng nhập</label>
                <input 
                  type="text" 
                  className="form-input" 
                  value={userForm.username}
                  onChange={e => setUserForm({...userForm, username: e.target.value})}
                  required 
                />
              </div>
              <div className="form-group">
                <label className="form-label">Email</label>
                <input 
                  type="email" 
                  className="form-input" 
                  value={userForm.email}
                  onChange={e => setUserForm({...userForm, email: e.target.value})}
                  required 
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mật khẩu</label>
                <input 
                  type="password" 
                  className="form-input" 
                  value={userForm.password}
                  onChange={e => setUserForm({...userForm, password: e.target.value})}
                  required 
                />
              </div>
              <div className="form-group">
                <label className="form-label">Vai trò</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', marginTop: '0.5rem' }}>
                  {roles.map(role => (
                    <label key={role.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem', cursor: 'pointer' }}>
                      <input 
                        type="checkbox" 
                        checked={userForm.roleNames.includes(role.name)}
                        onChange={() => toggleUserRole(role.name)}
                      />
                      {role.name}
                    </label>
                  ))}
                </div>
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowUserModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Tạo người dùng</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Role Creation Modal */}
      {showRoleModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Thêm vai trò mới</h2>
            <form onSubmit={handleCreateRole}>
              <div className="form-group">
                <label className="form-label">Tên vai trò</label>
                <input 
                  type="text" 
                  className="form-input" 
                  value={roleForm.name}
                  onChange={e => setRoleForm({...roleForm, name: e.target.value})}
                  placeholder="Ví dụ: Accountant, Manager..."
                  required 
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mô tả</label>
                <textarea 
                  className="form-input" 
                  style={{ minHeight: '80px', paddingTop: '0.5rem' }}
                  value={roleForm.description}
                  onChange={e => setRoleForm({...roleForm, description: e.target.value})}
                  placeholder="Mô tả chức năng của vai trò này"
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowRoleModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Tạo vai trò</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Permission Management Modal */}
      {showPermissionModal && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ maxWidth: '600px', maxHeight: '80vh', display: 'flex', flexDirection: 'column' }}>
            <h2 style={{ marginBottom: '0.5rem' }}>Quản lý quyền: {selectedRole?.name}</h2>
            <p style={{ color: '#94a3b8', fontSize: '0.875rem', marginBottom: '1.5rem' }}>Chọn các quyền hạn được phép cho vai trò này.</p>
            
            <div style={{ flex: 1, overflowY: 'auto', marginBottom: '1.5rem', paddingRight: '0.5rem' }}>
              {/* Group by Scope */}
              {['Menu', 'Page', 'Action', 'Field'].map(scope => {
                const perms = allPermissions.filter(p => p.scope === scope);
                if (perms.length === 0) return null;
                return (
                  <div key={scope} style={{ marginBottom: '1.5rem' }}>
                    <h4 style={{ color: '#38bdf8', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '0.75rem', borderBottom: '1px solid rgba(56, 189, 248, 0.2)', paddingBottom: '0.25rem' }}>
                      {scope} Permissions
                    </h4>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.5rem' }}>
                      {perms.map(p => (
                        <label key={p.id} style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.5rem', borderRadius: '0.5rem', cursor: 'pointer', background: 'rgba(255,255,255,0.02)' }}>
                          <input 
                            type="checkbox" 
                            checked={selectedPermissionIds.includes(p.id)}
                            onChange={() => togglePermission(p.id)}
                            style={{ width: '18px', height: '18px', accentColor: '#2563eb' }}
                          />
                          <div>
                            <div style={{ fontSize: '0.875rem', fontWeight: 500 }}>{p.name}</div>
                            <div style={{ fontSize: '0.75rem', color: '#64748b' }}>{p.code}</div>
                          </div>
                        </label>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>

            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', paddingTop: '1rem', borderTop: '1px solid rgba(255,255,255,0.1)' }}>
              <button type="button" className="btn btn-outline" onClick={() => setShowPermissionModal(false)}>Hủy</button>
              <button type="button" className="btn btn-primary" onClick={handleSavePermissions}>Lưu thay đổi</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};


const AuditLogViewer = ({ api }) => {
  const [logs, setLogs] = useState([]);
  const [selectedLog, setSelectedLog] = useState(null);
  const [filters, setFilters] = useState({ userId: '', action: '' });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchLogs();
  }, []);

  const fetchLogs = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/audit', { params: filters });
      setLogs(res.data.items || []);
    } catch (error) {
      toast.error('Lỗi khi tải nhật ký Audit');
    } finally {
      setLoading(false);
    }
  };

  const getActionColor = (action) => {
    if (action.includes('Create') || action.includes('Add')) return '#22c55e';
    if (action.includes('Update') || action.includes('Modify')) return '#38bdf8';
    if (action.includes('Delete') || action.includes('Remove')) return '#ef4444';
    return '#94a3b8';
  };

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
        <h2 style={{ fontSize: '1.25rem' }}>Nhật ký hệ thống (Audit Logs)</h2>
        <div style={{ display: 'flex', gap: '1rem' }}>
           <button className="btn btn-outline" onClick={() => api.get('/api/v1/audit/verify-integrity').then(() => toast.success('Xác thực chuỗi Hash thành công!'))}>
            <ShieldCheck size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
            Kiểm tra tính toàn vẹn
          </button>
          <button className="btn btn-outline" onClick={fetchLogs}>
            <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
            Làm mới
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: selectedLog ? '1fr 1fr' : '1fr', gap: '2rem' }}>
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr>
                <th>Thời gian</th>
                <th>Người dùng</th>
                <th>Hành động</th>
                <th>Đối tượng</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {logs.map(log => (
                <tr key={log.id} 
                    style={{ cursor: 'pointer', background: selectedLog?.id === log.id ? 'rgba(56, 189, 248, 0.05)' : 'transparent' }}
                    onClick={() => setSelectedFlow(null) || setSelectedLog(log)}>
                  <td style={{ fontSize: '0.875rem', whiteSpace: 'nowrap' }}>{new Date(log.timestampUtc).toLocaleString()}</td>
                  <td>{log.userName || 'System'}</td>
                  <td>
                    <span className="status-badge" style={{ background: `${getActionColor(log.action)}20`, color: getActionColor(log.action) }}>
                      {log.action}
                    </span>
                  </td>
                  <td>{log.entityName}</td>
                  <td><ChevronRight size={16} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {selectedLog && (
          <div style={{ borderLeft: '1px solid rgba(255, 255, 255, 0.1)', paddingLeft: '2rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <h3 style={{ marginBottom: '1.5rem', color: '#94a3b8' }}>Chi tiết thay đổi</h3>
              <button className="btn btn-outline" style={{ padding: '4px' }} onClick={() => setSelectedLog(null)}>×</button>
            </div>
            
            <div style={{ marginBottom: '1.5rem' }}>
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', marginBottom: '0.25rem' }}>TRANSACTION ID</div>
              <div style={{ fontFamily: 'monospace', fontSize: '0.875rem' }}>{selectedLog.transactionId}</div>
            </div>

            <div className="diff-container">
              <div>
                <div className="diff-label">Dữ liệu cũ (Old)</div>
                <pre className="diff-box" style={{ color: '#ef4444' }}>
                  {selectedLog.oldValues ? JSON.stringify(JSON.parse(selectedLog.oldValues), null, 2) : 'N/A'}
                </pre>
              </div>
              <div>
                <div className="diff-label">Dữ liệu mới (New)</div>
                <pre className="diff-box" style={{ color: '#22c55e' }}>
                  {selectedLog.newValues ? JSON.stringify(JSON.parse(selectedLog.newValues), null, 2) : 'N/A'}
                </pre>
              </div>
            </div>

            {selectedLog.affectedColumns && (
              <div style={{ marginTop: '1.5rem' }}>
                <div className="diff-label">Các trường bị ảnh hưởng</div>
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                  {JSON.parse(selectedLog.affectedColumns).map(col => (
                    <span key={col} className="status-badge" style={{ background: 'rgba(255, 255, 255, 0.05)', color: '#cbd5e1' }}>
                      {col}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

function App() {
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [authError, setAuthError] = useState('');
  const [loginData, setLoginData] = useState({ username: 'admin', password: 'Admin@123' });

  const [activeTab, setActiveTab] = useState('dashboard');
  const [invoices, setInvoices] = useState([]);
  const [stats, setStats] = useState({
    totalInvoices: 0,
    totalRevenue: 0,
    paidInvoices: 0,
    pendingInvoices: 0
  });
  const [showModal, setShowModal] = useState(false);
  const [newInvoice, setNewInvoice] = useState({ customerName: '', amount: '' });

  // Axios configuration with token
  const api = axios.create({
    baseURL: GATEWAY_URL,
    headers: {
      Authorization: `Bearer ${token}`
    }
  });

  useEffect(() => {
    if (token) {
      fetchData();
      const interval = setInterval(fetchData, 5000);
      return () => clearInterval(interval);
    }
  }, [token]);

  const fetchData = async () => {
    try {
      const invRes = await api.get('/api/v1/invoice');
      setInvoices(invRes.data);

      const statsRes = await api.get('/api/v1/report/summary');
      setStats(statsRes.data);
    } catch (error) {
      console.error('Error fetching data:', error);
      if (error.response?.status === 401) handleLogout();
    }
  };

  const handleLogin = async (e) => {
    e.preventDefault();
    setAuthError('');
    try {
      const res = await axios.post(`${GATEWAY_URL}/api/v1/auth/login`, loginData);
      const newToken = res.data.accessToken;
      setToken(newToken);
      localStorage.setItem('token', newToken);
      toast.success('Chào mừng quay trở lại, ' + loginData.username + '!');
    } catch (error) {
      setAuthError('Tên đăng nhập hoặc mật khẩu không đúng');
      toast.error('Đăng nhập thất bại');
    }
  };

  const handleLogout = () => {
    setToken(null);
    localStorage.removeItem('token');
    toast.success('Đã đăng xuất an toàn');
  };

  const handleCreateInvoice = async (e) => {
    e.preventDefault();
    try {
      const amount = Number(newInvoice.amount);
      if (!newInvoice.customerName.trim()) {
        toast.error('Tên khách hàng không được để trống');
        return;
      }
      if (!Number.isFinite(amount) || amount <= 0) {
        toast.error('Số tiền phải lớn hơn 0');
        return;
      }

      await api.post('/api/v1/invoice', {
        customerName: newInvoice.customerName.trim(),
        amount
      });
      setShowModal(false);
      setNewInvoice({ customerName: '', amount: '' });
      fetchData();
      toast.success('Hóa đơn đã được tạo thành công');
    } catch (error) {
      console.error('Error creating invoice:', error);
      const serverMessage =
        error.response?.data?.error ||
        error.response?.data?.message ||
        error.response?.data?.Message ||
        (Array.isArray(error.response?.data?.errors)
          ? error.response.data.errors.join(', ')
          : null);
      toast.error(serverMessage || 'Lỗi khi tạo hóa đơn');
    }
  };

  const handlePay = async (invoiceId, amount) => {
    const toastId = toast.loading('Đang xử lý thanh toán...');
    try {
      // Generate a simple idempotency key for the demo
      const idempotencyKey = `pay_${invoiceId}_${new Date().getTime()}`;
      
      await api.post('/api/v1/payment/pay', {
        invoiceId,
        amount
      }, {
        headers: {
          'X-Idempotency-Key': idempotencyKey
        }
      });
      fetchData();
      toast.success('Thanh toán hoàn tất!', { id: toastId });
    } catch (error) {
      console.error('Error processing payment:', error);
      toast.error('Thanh toán thất bại', { id: toastId });
    }
  };

  if (!token) {
    return (
      <div className="login-container" style={{ display: 'flex', height: '100vh', alignItems: 'center', justifyContent: 'center', background: '#f8fafc' }}>
        <div className="login-card" style={{ background: 'white', padding: '2.5rem', borderRadius: '1rem', boxShadow: '0 10px 25px rgba(0,0,0,0.05)', width: '100%', maxWidth: '400px' }}>
          <div style={{ textAlign: 'center', marginBottom: '2rem' }}>
            <LayoutDashboard size={48} color="#2563eb" style={{ marginBottom: '1rem' }} />
            <h1 style={{ fontSize: '1.5rem', fontWeight: 700, color: '#1e293b' }}>BizCore ERP</h1>
            <p style={{ color: '#64748b', marginTop: '0.5rem' }}>Đăng nhập để quản trị hệ thống</p>
          </div>
          
          <form onSubmit={handleLogin}>
            <div className="form-group" style={{ marginBottom: '1.25rem' }}>
              <label className="form-label">Tên đăng nhập</label>
              <input 
                type="text" 
                className="form-input" 
                value={loginData.username}
                onChange={e => setLoginData({...loginData, username: e.target.value})}
                required 
              />
            </div>
            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
              <label className="form-label">Mật khẩu</label>
              <input 
                type="password" 
                className="form-input" 
                value={loginData.password}
                onChange={e => setLoginData({...loginData, password: e.target.value})}
                required 
              />
            </div>
            
            {authError && <div style={{ color: '#ef4444', fontSize: '0.875rem', marginBottom: '1rem', textAlign: 'center' }}>{authError}</div>}
            
            <button type="submit" className="btn btn-primary" style={{ width: '100%', padding: '0.75rem', fontSize: '1rem' }}>
              Đăng nhập ngay
            </button>
            
            <div style={{ marginTop: '1.5rem', textAlign: 'center', fontSize: '0.875rem', color: '#94a3b8' }}>
              Demo: admin / Admin@123
            </div>
          </form>
        </div>
      </div>
    );
  }

  return (
    <div className="app-container">
      <Toaster position="top-right" reverseOrder={false} />
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="logo">
          <LayoutDashboard size={28} />
          <span>BizCore ERP</span>
        </div>
        <nav style={{ flex: 1 }}>
          <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
            <BarChart3 size={20} /> Dashboard
          </div>
          <div className={`nav-item ${activeTab === 'invoices' ? 'active' : ''}`} onClick={() => setActiveTab('invoices')}>
            <FileText size={20} /> Hóa đơn
          </div>
          <div className={`nav-item ${activeTab === 'orchestration' ? 'active' : ''}`} onClick={() => setActiveTab('orchestration')}>
            <Workflow size={20} /> Orchestration
          </div>
          <div className={`nav-item ${activeTab === 'identity' ? 'active' : ''}`} onClick={() => setActiveTab('identity')}>
            <ShieldCheck size={20} /> Phân quyền
          </div>
          <div className={`nav-item ${activeTab === 'audit' ? 'active' : ''}`} onClick={() => setActiveTab('audit')}>
            <Activity size={20} /> Audit Logs
          </div>
        </nav>
        <div className="nav-item logout" onClick={handleLogout} style={{ borderTop: '1px solid #334155', paddingTop: '1.5rem', marginTop: 'auto', color: '#94a3b8' }}>
          <CheckCircle2 size={20} /> Đăng xuất
        </div>
      </aside>

      {/* Main Content */}
      <main className="main-content">
        <header className="header">
          <h1 className="title">
            {activeTab === 'dashboard' && 'Tổng quan hệ thống'}
            {activeTab === 'invoices' && 'Quản lý hóa đơn'}
            {activeTab === 'orchestration' && 'Luồng nghiệp vụ'}
            {activeTab === 'identity' && 'Quản trị danh tính'}
            {activeTab === 'audit' && 'Truy vết hệ thống'}
          </h1>
          {activeTab === 'invoices' && (
            <button className="btn btn-primary" onClick={() => setShowModal(true)}>
              <Plus size={20} style={{ verticalAlign: 'middle', marginRight: '5px' }} />
              Tạo hóa đơn mới
            </button>
          )}
        </header>

        {activeTab === 'dashboard' ? (
          <>
            <div className="stats-grid">
              <div className="stat-card">
                <div className="stat-label">Tổng hóa đơn</div>
                <div className="stat-value">{stats.totalInvoices}</div>
              </div>
              <div className="stat-card">
                <div className="stat-label">Doanh thu (Paid)</div>
                <div className="stat-value" style={{ color: '#22c55e' }}>${stats.totalRevenue.toLocaleString()}</div>
              </div>
              <div className="stat-card">
                <div className="stat-label">Đã thanh toán</div>
                <div className="stat-value">{stats.paidInvoices}</div>
              </div>
              <div className="stat-card">
                <div className="stat-label">Chờ thanh toán</div>
                <div className="stat-value" style={{ color: '#eab308' }}>{stats.pendingInvoices}</div>
              </div>
            </div>

            <div className="card">
              <h2 style={{ marginBottom: '1.5rem', fontSize: '1.25rem' }}>Giao dịch gần đây</h2>
              <table className="table">
                <thead>
                  <tr>
                    <th>Khách hàng</th>
                    <th>Số tiền</th>
                    <th>Ngày tạo</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {invoices.slice(0, 5).map(inv => (
                    <tr key={inv.id}>
                      <td>{inv.customerName}</td>
                      <td>${inv.amount}</td>
                      <td>{new Date(inv.createdAt).toLocaleDateString()}</td>
                      <td>
                        <span className={`status-badge ${inv.status === 1 ? 'status-paid' : 'status-pending'}`}>
                          {inv.status === 1 ? 'PAID' : 'PENDING'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : activeTab === 'invoices' ? (
          <div className="card">
            <table className="table">
              <thead>
                <tr>
                  <th>Mã hóa đơn</th>
                  <th>Khách hàng</th>
                  <th>Số tiền</th>
                  <th>Ngày tạo</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map(inv => (
                  <tr key={inv.id}>
                    <td style={{ fontSize: '0.75rem', opacity: 0.6 }}>{inv.id}</td>
                    <td>{inv.customerName}</td>
                    <td>${inv.amount}</td>
                    <td>{new Date(inv.createdAt).toLocaleDateString()}</td>
                    <td>
                      <span className={`status-badge ${inv.status === 1 ? 'status-paid' : 'status-pending'}`}>
                        {inv.status === 1 ? 'PAID' : 'PENDING'}
                      </span>
                    </td>
                    <td>
                      {inv.status === 0 && (
                        <button className="btn btn-primary" onClick={() => handlePay(inv.id, inv.amount)}>
                          Thanh toán ngay
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : activeTab === 'orchestration' ? (
          <OrchestrationFlow api={api} />
        ) : activeTab === 'identity' ? (
          <IdentityManager api={api} />
        ) : activeTab === 'audit' ? (
          <AuditLogViewer api={api} />
        ) : null}
      </main>

      {/* Modal Tạo Hóa Đơn */}
      {showModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Tạo hóa đơn mới</h2>
            <form onSubmit={handleCreateInvoice}>
              <div className="form-group">
                <label className="form-label">Tên khách hàng</label>
                <input 
                  type="text" 
                  className="form-input" 
                  value={newInvoice.customerName}
                  onChange={e => setNewInvoice({...newInvoice, customerName: e.target.value})}
                  required 
                />
              </div>
              <div className="form-group">
                <label className="form-label">Số tiền ($)</label>
                <input 
                  type="number" 
                  className="form-input" 
                  value={newInvoice.amount}
                  min="0.01"
                  step="0.01"
                  onChange={e => setNewInvoice({...newInvoice, amount: e.target.value})}
                  required 
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Xác nhận tạo</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;

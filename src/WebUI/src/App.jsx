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
  UserPlus,
  Upload,
  User as UserIcon,
  Building2,
  ShoppingCart,
  Package,
  Warehouse,
  X
} from 'lucide-react';
import { Toaster, toast } from 'react-hot-toast';
import { useTranslation } from 'react-i18next';
import i18n from './i18n';

import * as signalR from '@microsoft/signalr';

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
  const [showUserRolesModal, setShowUserRolesModal] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [selectedRoleIds, setSelectedRoleIds] = useState([]);
  const [userForm, setUserForm] = useState({ username: '', email: '', password: '', roleNames: [] });

  useEffect(() => {
    if (activeSubTab === 'users') {
      fetchUsers();
      if (roles.length === 0) fetchRoles();
    } else {
      fetchRoles();
    }
  }, [activeSubTab]);

  const handleAvatarUpload = async (userId, file) => {
    if (!file) return;
    
    const formData = new FormData();
    formData.append('file', file);

    const toastId = toast.loading('Đang tải ảnh lên...');
    try {
      // 1. Upload file to File.API as PUBLIC
      // Using ?isPublic=true to get a permanent URL for the avatar
      const uploadRes = await api.post('/api/v1/files/upload?isPublic=true', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      
      // The updated backend returns the permanent URL directly if isPublic is true
      const avatarUrl = uploadRes.data.url;

      // 2. Update User Avatar in Admin.API with the permanent URL
      await api.put(`/api/v1/users/${userId}/avatar`, JSON.stringify(avatarUrl), {
        headers: { 'Content-Type': 'application/json' }
      });

      toast.success('Cập nhật ảnh đại diện thành công', { id: toastId });
      fetchUsers();
    } catch (error) {
      toast.error('Lỗi khi tải ảnh: ' + getErrorDetail(error), { id: toastId });
    }
  };

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/users');
      setUsers(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách người dùng: ' + getErrorDetail(error));
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
      toast.error('Lỗi khi tải danh sách vai trò: ' + getErrorDetail(error));
    } finally {
      setLoading(false);
    }
  };

  const fetchAllPermissions = async () => {
    try {
      const res = await api.get('/api/v1/roles/permissions');
      setAllPermissions(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách quyền: ' + getErrorDetail(error));
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
      toast.error(getErrorDetail(error));
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
      toast.error(getErrorDetail(error));
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
      toast.error('Lỗi khi cập nhật quyền: ' + getErrorDetail(error));
    }
  };

  const togglePermission = (id) => {
    setSelectedPermissionIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  const openUserRolesModal = (user) => {
    setSelectedUser(user);
    // Map current user role names to role IDs
    const currentRoleIds = roles
      .filter(r => user.roles?.includes(r.name))
      .map(r => r.id);
    setSelectedRoleIds(currentRoleIds);
    setShowUserRolesModal(true);
  };

  const handleSaveUserRoles = async () => {
    try {
      await api.put(`/api/v1/users/${selectedUser.id}/roles`, {
        roleIds: selectedRoleIds
      });
      toast.success('Cập nhật vai trò người dùng thành công');
      setShowUserRolesModal(false);
      fetchUsers();
    } catch (error) {
      toast.error('Lỗi khi cập nhật vai trò: ' + getErrorDetail(error));
    }
  };

  const toggleRoleAssignment = (roleId) => {
    setSelectedRoleIds(prev => 
      prev.includes(roleId) ? prev.filter(id => id !== roleId) : [...prev, roleId]
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
                <th style={{ width: '50px' }}>Ảnh</th>
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
                  <td>
                    <div style={{ position: 'relative', width: '36px', height: '36px' }}>
                      <div style={{ 
                        width: '36px', 
                        height: '36px', 
                        borderRadius: '50%', 
                        overflow: 'hidden', 
                        background: 'rgba(255,255,255,0.05)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        border: '1px solid rgba(255,255,255,0.1)'
                      }}>
                        {user.avatarUrl ? (
                          <img src={user.avatarUrl} alt="Avatar" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        ) : (
                          <UserIcon size={16} color="#64748b" />
                        )}
                      </div>
                      <label style={{ 
                        position: 'absolute', 
                        bottom: '-2px', 
                        right: '-2px', 
                        background: '#2563eb', 
                        borderRadius: '50%', 
                        width: '16px', 
                        height: '16px', 
                        display: 'flex', 
                        alignItems: 'center', 
                        justifyContent: 'center',
                        cursor: 'pointer',
                        border: '2px solid #0f172a'
                      }}>
                        <Plus size={10} color="white" />
                        <input 
                          type="file" 
                          hidden 
                          accept="image/*" 
                          onChange={(e) => handleAvatarUpload(user.id, e.target.files[0])} 
                        />
                      </label>
                    </div>
                  </td>
                  <td style={{ fontWeight: 600 }}>{user.username}</td>
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
                  <td>
                    <button 
                      className="btn btn-outline" 
                      style={{ padding: '4px 8px' }}
                      onClick={() => openUserRolesModal(user)}
                    >
                      Phân quyền
                    </button>
                  </td>
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
              {/* Group by Resource */}
              {Array.from(new Set(allPermissions.map(p => p.resource))).sort().map(resource => {
                const resourcePerms = allPermissions.filter(p => p.resource === resource);
                return (
                  <div key={resource} style={{ 
                    marginBottom: '2rem', 
                    background: 'rgba(255,255,255,0.02)', 
                    padding: '1rem', 
                    borderRadius: '0.75rem',
                    border: '1px solid rgba(255,255,255,0.05)'
                  }}>
                    <h3 style={{ 
                      color: '#38bdf8', 
                      fontSize: '1rem', 
                      marginBottom: '1rem',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '0.5rem'
                    }}>
                      <div style={{ width: '4px', height: '16px', background: '#38bdf8', borderRadius: '2px' }}></div>
                      {resource} Module
                    </h3>

                    {/* Sub-group by Scope */}
                    {['Menu', 'Page', 'Action', 'Field'].map(scope => {
                      const scopePerms = resourcePerms.filter(p => p.scope === scope);
                      if (scopePerms.length === 0) return null;
                      return (
                        <div key={scope} style={{ marginBottom: '1rem', marginLeft: '1rem' }}>
                          <h4 style={{ 
                            color: '#94a3b8', 
                            fontSize: '0.7rem', 
                            textTransform: 'uppercase', 
                            letterSpacing: '0.05em', 
                            marginBottom: '0.5rem',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem'
                          }}>
                            {scope} Permissions
                          </h4>
                          <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.4rem' }}>
                            {scopePerms.map(p => (
                              <label key={p.id} style={{ 
                                display: 'flex', 
                                alignItems: 'center', 
                                gap: '0.75rem', 
                                padding: '0.6rem', 
                                borderRadius: '0.5rem', 
                                cursor: 'pointer', 
                                background: 'rgba(255,255,255,0.03)',
                                transition: 'all 0.2s'
                              }} className="permission-item-hover">
                                <input 
                                  type="checkbox" 
                                  checked={selectedPermissionIds.includes(p.id)}
                                  onChange={() => togglePermission(p.id)}
                                  style={{ width: '18px', height: '18px', accentColor: '#2563eb' }}
                                />
                                <div>
                                  <div style={{ fontSize: '0.875rem', fontWeight: 500 }}>{p.name}</div>
                                  <div style={{ fontSize: '0.7rem', color: '#64748b', fontFamily: 'monospace' }}>{p.code}</div>
                                </div>
                              </label>
                            ))}
                          </div>
                        </div>
                      );
                    })}
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

      {/* User Roles Management Modal */}
      {showUserRolesModal && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ maxWidth: '500px' }}>
            <h2 style={{ marginBottom: '0.5rem' }}>Phân vai trò: {selectedUser?.username}</h2>
            <p style={{ color: '#94a3b8', fontSize: '0.875rem', marginBottom: '1.5rem' }}>Chọn các vai trò áp dụng cho người dùng này.</p>
            
            <div style={{ maxHeight: '400px', overflowY: 'auto', marginBottom: '1.5rem' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.75rem' }}>
                {roles.map(role => (
                  <label key={role.id} style={{ 
                    display: 'flex', 
                    alignItems: 'center', 
                    gap: '0.75rem', 
                    padding: '0.75rem', 
                    borderRadius: '0.5rem', 
                    cursor: 'pointer', 
                    background: 'rgba(255,255,255,0.03)',
                    border: '1px solid rgba(255,255,255,0.05)'
                  }}>
                    <input 
                      type="checkbox" 
                      checked={selectedRoleIds.includes(role.id)}
                      onChange={() => toggleRoleAssignment(role.id)}
                      style={{ width: '18px', height: '18px', accentColor: '#2563eb' }}
                    />
                    <div>
                      <div style={{ fontSize: '0.875rem', fontWeight: 500 }}>{role.name}</div>
                      <div style={{ fontSize: '0.75rem', color: '#64748b' }}>{role.description}</div>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', paddingTop: '1rem', borderTop: '1px solid rgba(255,255,255,0.1)' }}>
              <button type="button" className="btn btn-outline" onClick={() => setShowUserRolesModal(false)}>Hủy</button>
              <button type="button" className="btn btn-primary" onClick={handleSaveUserRoles}>Lưu thay đổi</button>
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
      toast.error('Lỗi khi tải nhật ký Audit: ' + getErrorDetail(error));
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
                    onClick={() => setSelectedLog(log)}>
                  <td style={{ fontSize: '0.875rem', whiteSpace: 'nowrap' }}>{new Date(log.performedAt).toLocaleString()}</td>
                  <td>{log.performedByName || 'System'}</td>
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
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', marginBottom: '0.25rem' }}>CORRELATION ID</div>
              <div style={{ fontFamily: 'monospace', fontSize: '0.875rem' }}>{selectedLog.correlationId}</div>
            </div>

            <div className="diff-container">
              <div>
                <div className="diff-label">Dữ liệu cũ (Before)</div>
                <pre className="diff-box" style={{ color: '#ef4444' }}>
                  {selectedLog.beforeJson ? JSON.stringify(JSON.parse(selectedLog.beforeJson), null, 2) : 'N/A'}
                </pre>
              </div>
              <div>
                <div className="diff-label">Dữ liệu mới (After)</div>
                <pre className="diff-box" style={{ color: '#22c55e' }}>
                  {selectedLog.afterJson ? JSON.stringify(JSON.parse(selectedLog.afterJson), null, 2) : 'N/A'}
                </pre>
              </div>
            </div>

          </div>
        )}
      </div>
    </div>
  );
};

const CustomerManager = ({ api, getErrorDetail }) => {
  const [activeSubTab, setActiveSubTab] = useState('customers');
  const [customers, setCustomers] = useState([]);
  const [groups, setGroups] = useState([]);
  const [loading, setLoading] = useState(false);

  const emptyCustomerForm = { code: '', name: '', customerGroupId: '', taxCode: '', email: '', phone: '', address: '' };
  const emptyGroupForm = { code: '', name: '', description: '' };

  const [showCustomerModal, setShowCustomerModal] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState(null);
  const [customerForm, setCustomerForm] = useState(emptyCustomerForm);

  const [showGroupModal, setShowGroupModal] = useState(false);
  const [editingGroup, setEditingGroup] = useState(null);
  const [groupForm, setGroupForm] = useState(emptyGroupForm);

  useEffect(() => {
    fetchGroups();
    fetchCustomers();
  }, []);

  const fetchCustomers = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/customers');
      setCustomers(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách khách hàng: ' + getErrorDetail(error));
    } finally {
      setLoading(false);
    }
  };

  const fetchGroups = async () => {
    try {
      const res = await api.get('/api/v1/customer-groups');
      setGroups(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách nhóm khách hàng: ' + getErrorDetail(error));
    }
  };

  const openCreateCustomer = () => {
    if (groups.length === 0) {
      toast.error('Vui lòng tạo Nhóm khách hàng trước khi thêm khách hàng');
      return;
    }
    setEditingCustomer(null);
    setCustomerForm({ ...emptyCustomerForm, customerGroupId: groups[0]?.id || '' });
    setShowCustomerModal(true);
  };

  const openEditCustomer = (customer) => {
    setEditingCustomer(customer);
    setCustomerForm({
      code: customer.code,
      name: customer.name,
      customerGroupId: customer.customerGroupId,
      taxCode: customer.taxCode || '',
      email: customer.email || '',
      phone: customer.phone || '',
      address: customer.address || ''
    });
    setShowCustomerModal(true);
  };

  const handleSaveCustomer = async (e) => {
    e.preventDefault();
    try {
      if (editingCustomer) {
        await api.put(`/api/v1/customers/${editingCustomer.id}`, {
          name: customerForm.name,
          taxCode: customerForm.taxCode || null,
          email: customerForm.email || null,
          phone: customerForm.phone || null,
          address: customerForm.address || null
        });
        if (customerForm.customerGroupId !== editingCustomer.customerGroupId) {
          await api.put(`/api/v1/customers/${editingCustomer.id}/group`, {
            customerGroupId: customerForm.customerGroupId
          });
        }
        toast.success('Cập nhật khách hàng thành công');
      } else {
        await api.post('/api/v1/customers', {
          code: customerForm.code,
          name: customerForm.name,
          customerGroupId: customerForm.customerGroupId,
          taxCode: customerForm.taxCode || null,
          email: customerForm.email || null,
          phone: customerForm.phone || null,
          address: customerForm.address || null
        });
        toast.success('Tạo khách hàng thành công');
      }
      setShowCustomerModal(false);
      fetchCustomers();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const toggleCustomerStatus = async (customer) => {
    try {
      const action = customer.isActive ? 'deactivate' : 'activate';
      await api.post(`/api/v1/customers/${customer.id}/${action}`);
      toast.success(customer.isActive ? 'Đã ngừng hoạt động khách hàng' : 'Đã kích hoạt khách hàng');
      fetchCustomers();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const openCreateGroup = () => {
    setEditingGroup(null);
    setGroupForm(emptyGroupForm);
    setShowGroupModal(true);
  };

  const openEditGroup = (group) => {
    setEditingGroup(group);
    setGroupForm({ code: group.code, name: group.name, description: group.description || '' });
    setShowGroupModal(true);
  };

  const handleSaveGroup = async (e) => {
    e.preventDefault();
    try {
      if (editingGroup) {
        await api.put(`/api/v1/customer-groups/${editingGroup.id}`, {
          name: groupForm.name,
          description: groupForm.description || null
        });
        toast.success('Cập nhật nhóm khách hàng thành công');
      } else {
        await api.post('/api/v1/customer-groups', {
          code: groupForm.code,
          name: groupForm.name,
          description: groupForm.description || null
        });
        toast.success('Tạo nhóm khách hàng thành công');
      }
      setShowGroupModal(false);
      fetchGroups();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const toggleGroupStatus = async (group) => {
    try {
      const action = group.isActive ? 'deactivate' : 'activate';
      await api.post(`/api/v1/customer-groups/${group.id}/${action}`);
      toast.success(group.isActive ? 'Đã ngừng hoạt động nhóm khách hàng' : 'Đã kích hoạt nhóm khách hàng');
      fetchGroups();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  return (
    <div className="card">
      <div className="tab-header">
        <div className={`tab-btn ${activeSubTab === 'customers' ? 'active' : ''}`} onClick={() => setActiveSubTab('customers')}>
          <Users size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Khách hàng
        </div>
        <div className={`tab-btn ${activeSubTab === 'groups' ? 'active' : ''}`} onClick={() => setActiveSubTab('groups')}>
          <Building2 size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Nhóm khách hàng
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginBottom: '1rem' }}>
        <button className="btn btn-outline" onClick={() => activeSubTab === 'customers' ? fetchCustomers() : fetchGroups()} disabled={loading}>
          <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Làm mới
        </button>
        <button className="btn btn-primary" onClick={() => activeSubTab === 'customers' ? openCreateCustomer() : openCreateGroup()}>
          <Plus size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Thêm {activeSubTab === 'customers' ? 'Khách hàng' : 'Nhóm khách hàng'}
        </button>
      </div>

      <div className="table-wrapper">
        {activeSubTab === 'customers' ? (
          <table className="table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên khách hàng</th>
                <th>Nhóm</th>
                <th>Email</th>
                <th>Điện thoại</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {customers.map(c => (
                <tr key={c.id}>
                  <td style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{c.code}</td>
                  <td style={{ fontWeight: 600 }}>{c.name}</td>
                  <td>{c.customerGroupName}</td>
                  <td>{c.email || '—'}</td>
                  <td>{c.phone || '—'}</td>
                  <td>
                    <span className={`status-badge ${c.isActive ? 'status-paid' : 'status-pending'}`}>
                      {c.isActive ? 'Hoạt động' : 'Ngừng hoạt động'}
                    </span>
                  </td>
                  <td style={{ display: 'flex', gap: '0.5rem' }}>
                    <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => openEditCustomer(c)}>
                      Sửa
                    </button>
                    <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => toggleCustomerStatus(c)}>
                      {c.isActive ? 'Ngừng' : 'Kích hoạt'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên nhóm</th>
                <th>Mô tả</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {groups.map(g => (
                <tr key={g.id}>
                  <td style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{g.code}</td>
                  <td style={{ fontWeight: 600 }}>{g.name}</td>
                  <td style={{ color: '#94a3b8', fontSize: '0.875rem' }}>{g.description || '—'}</td>
                  <td>
                    <span className={`status-badge ${g.isActive ? 'status-paid' : 'status-pending'}`}>
                      {g.isActive ? 'Hoạt động' : 'Ngừng hoạt động'}
                    </span>
                  </td>
                  <td style={{ display: 'flex', gap: '0.5rem' }}>
                    <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => openEditGroup(g)}>
                      Sửa
                    </button>
                    <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => toggleGroupStatus(g)}>
                      {g.isActive ? 'Ngừng' : 'Kích hoạt'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Customer Create/Edit Modal */}
      {showCustomerModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>{editingCustomer ? 'Cập nhật khách hàng' : 'Thêm khách hàng mới'}</h2>
            <form onSubmit={handleSaveCustomer}>
              <div className="form-group">
                <label className="form-label">Mã khách hàng</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.code}
                  onChange={e => setCustomerForm({ ...customerForm, code: e.target.value })}
                  disabled={!!editingCustomer}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Tên khách hàng</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.name}
                  onChange={e => setCustomerForm({ ...customerForm, name: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Nhóm khách hàng</label>
                <select
                  className="form-input"
                  value={customerForm.customerGroupId}
                  onChange={e => setCustomerForm({ ...customerForm, customerGroupId: e.target.value })}
                  required
                >
                  <option value="" disabled>Chọn nhóm khách hàng</option>
                  {groups.map(g => (
                    <option key={g.id} value={g.id}>{g.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label className="form-label">Mã số thuế</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.taxCode}
                  onChange={e => setCustomerForm({ ...customerForm, taxCode: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Email</label>
                <input
                  type="email"
                  className="form-input"
                  value={customerForm.email}
                  onChange={e => setCustomerForm({ ...customerForm, email: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Điện thoại</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.phone}
                  onChange={e => setCustomerForm({ ...customerForm, phone: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Địa chỉ</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.address}
                  onChange={e => setCustomerForm({ ...customerForm, address: e.target.value })}
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowCustomerModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">{editingCustomer ? 'Lưu thay đổi' : 'Tạo khách hàng'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CustomerGroup Create/Edit Modal */}
      {showGroupModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>{editingGroup ? 'Cập nhật nhóm khách hàng' : 'Thêm nhóm khách hàng mới'}</h2>
            <form onSubmit={handleSaveGroup}>
              <div className="form-group">
                <label className="form-label">Mã nhóm</label>
                <input
                  type="text"
                  className="form-input"
                  value={groupForm.code}
                  onChange={e => setGroupForm({ ...groupForm, code: e.target.value })}
                  disabled={!!editingGroup}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Tên nhóm</label>
                <input
                  type="text"
                  className="form-input"
                  value={groupForm.name}
                  onChange={e => setGroupForm({ ...groupForm, name: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mô tả</label>
                <textarea
                  className="form-input"
                  style={{ minHeight: '80px', paddingTop: '0.5rem' }}
                  value={groupForm.description}
                  onChange={e => setGroupForm({ ...groupForm, description: e.target.value })}
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowGroupModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">{editingGroup ? 'Lưu thay đổi' : 'Tạo nhóm'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

const ProductManager = ({ api, getErrorDetail }) => {
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(false);

  const emptyProductForm = { name: '', unit: '', price: '', description: '' };
  const [showModal, setShowModal] = useState(false);
  const [editingProduct, setEditingProduct] = useState(null);
  const [productForm, setProductForm] = useState(emptyProductForm);

  useEffect(() => {
    fetchProducts();
  }, []);

  const fetchProducts = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/products');
      setProducts(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách sản phẩm: ' + getErrorDetail(error));
    } finally {
      setLoading(false);
    }
  };

  const openCreate = () => {
    setEditingProduct(null);
    setProductForm(emptyProductForm);
    setShowModal(true);
  };

  const openEdit = (product) => {
    setEditingProduct(product);
    setProductForm({
      name: product.name,
      unit: product.unit,
      price: product.price,
      description: product.description || ''
    });
    setShowModal(true);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const price = Number(productForm.price);
    if (!Number.isFinite(price) || price < 0) {
      toast.error('Giá bán không hợp lệ');
      return;
    }
    try {
      const payload = {
        name: productForm.name,
        unit: productForm.unit,
        price,
        description: productForm.description || null
      };
      if (editingProduct) {
        await api.put(`/api/v1/products/${editingProduct.id}`, payload);
        toast.success('Cập nhật sản phẩm thành công');
      } else {
        await api.post('/api/v1/products', payload);
        toast.success('Tạo sản phẩm thành công');
      }
      setShowModal(false);
      fetchProducts();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const toggleStatus = async (product) => {
    try {
      const action = product.isActive ? 'deactivate' : 'activate';
      await api.post(`/api/v1/products/${product.id}/${action}`);
      toast.success(product.isActive ? 'Đã ngừng kinh doanh sản phẩm' : 'Đã kích hoạt sản phẩm');
      fetchProducts();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginBottom: '1rem' }}>
        <button className="btn btn-outline" onClick={fetchProducts} disabled={loading}>
          <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Làm mới
        </button>
        <button className="btn btn-primary" onClick={openCreate}>
          <Plus size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Thêm sản phẩm
        </button>
      </div>

      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Mã</th>
              <th>Tên sản phẩm</th>
              <th>Đơn vị</th>
              <th>Giá bán</th>
              <th>Trạng thái</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {products.map(p => (
              <tr key={p.id}>
                <td style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{p.code}</td>
                <td style={{ fontWeight: 600 }}>{p.name}</td>
                <td>{p.unit}</td>
                <td>{p.price.toLocaleString()}</td>
                <td>
                  <span className={`status-badge ${p.isActive ? 'status-paid' : 'status-pending'}`}>
                    {p.isActive ? 'Đang kinh doanh' : 'Ngừng kinh doanh'}
                  </span>
                </td>
                <td style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => openEdit(p)}>
                    Sửa
                  </button>
                  <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => toggleStatus(p)}>
                    {p.isActive ? 'Ngừng' : 'Kích hoạt'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>{editingProduct ? 'Cập nhật sản phẩm' : 'Thêm sản phẩm mới'}</h2>
            <form onSubmit={handleSave}>
              <div className="form-group">
                <label className="form-label">Tên sản phẩm</label>
                <input
                  type="text"
                  className="form-input"
                  value={productForm.name}
                  onChange={e => setProductForm({ ...productForm, name: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Đơn vị tính</label>
                <input
                  type="text"
                  className="form-input"
                  placeholder="Cái, Kg, Hộp..."
                  value={productForm.unit}
                  onChange={e => setProductForm({ ...productForm, unit: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Giá bán</label>
                <input
                  type="number"
                  className="form-input"
                  min="0"
                  step="0.01"
                  value={productForm.price}
                  onChange={e => setProductForm({ ...productForm, price: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mô tả</label>
                <textarea
                  className="form-input"
                  style={{ minHeight: '80px', paddingTop: '0.5rem' }}
                  value={productForm.description}
                  onChange={e => setProductForm({ ...productForm, description: e.target.value })}
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">{editingProduct ? 'Lưu thay đổi' : 'Tạo sản phẩm'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

const stockTransactionTypeLabel = {
  Reserve: 'Giữ chỗ',
  Commit: 'Xuất kho (đơn xác nhận)',
  Release: 'Trả chỗ (đơn hủy)',
  Adjust: 'Điều chỉnh thủ công'
};

const InventoryManager = ({ api, getErrorDetail }) => {
  const [stocks, setStocks] = useState([]);
  const [loading, setLoading] = useState(false);

  const [showModal, setShowModal] = useState(false);
  const [editingStock, setEditingStock] = useState(null);
  const [quantityOnHand, setQuantityOnHand] = useState('');

  const [showHistory, setShowHistory] = useState(false);
  const [historyProduct, setHistoryProduct] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  useEffect(() => {
    fetchStocks();
  }, []);

  const fetchStocks = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/inventory');
      setStocks(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách tồn kho: ' + getErrorDetail(error));
    } finally {
      setLoading(false);
    }
  };

  const openAdjust = (stock) => {
    setEditingStock(stock);
    setQuantityOnHand(stock.quantityOnHand);
    setShowModal(true);
  };

  const openHistory = async (stock) => {
    setHistoryProduct(stock || null);
    setShowHistory(true);
    try {
      setHistoryLoading(true);
      const res = await api.get('/api/v1/inventory/transactions', {
        params: stock ? { productId: stock.productId } : {}
      });
      setTransactions(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải lịch sử xuất nhập tồn: ' + getErrorDetail(error));
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const qty = Number(quantityOnHand);
    if (!Number.isFinite(qty) || qty < 0) {
      toast.error('Tồn kho không hợp lệ');
      return;
    }
    try {
      await api.put(`/api/v1/inventory/${editingStock.productId}`, {
        productName: editingStock.productName,
        quantityOnHand: qty
      });
      toast.success('Cập nhật tồn kho thành công');
      setShowModal(false);
      fetchStocks();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginBottom: '1rem' }}>
        <button className="btn btn-outline" onClick={() => openHistory(null)}>
          <Clock size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Lịch sử xuất nhập tồn
        </button>
        <button className="btn btn-outline" onClick={fetchStocks} disabled={loading}>
          <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Làm mới
        </button>
      </div>

      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              <th>Sản phẩm</th>
              <th>Tồn kho</th>
              <th>Đã giữ chỗ</th>
              <th>Khả dụng</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stocks.map(s => (
              <tr key={s.id}>
                <td style={{ fontWeight: 600 }}>{s.productName}</td>
                <td>{s.quantityOnHand.toLocaleString()}</td>
                <td>{s.quantityReserved.toLocaleString()}</td>
                <td>
                  <span className={`status-badge ${s.availableQuantity < 0 ? 'status-pending' : 'status-paid'}`}>
                    {s.availableQuantity.toLocaleString()}
                  </span>
                </td>
                <td style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => openAdjust(s)}>
                    Điều chỉnh
                  </button>
                  <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => openHistory(s)}>
                    Lịch sử
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Điều chỉnh tồn kho: {editingStock?.productName}</h2>
            <form onSubmit={handleSave}>
              <div className="form-group">
                <label className="form-label">Tồn kho thực tế</label>
                <input
                  type="number"
                  className="form-input"
                  min="0"
                  value={quantityOnHand}
                  onChange={e => setQuantityOnHand(e.target.value)}
                  required
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Lưu thay đổi</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showHistory && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ maxWidth: '720px' }}>
            <h2 style={{ marginBottom: '1.5rem' }}>
              Lịch sử xuất nhập tồn{historyProduct ? `: ${historyProduct.productName}` : ' (tất cả sản phẩm)'}
            </h2>
            <div className="table-wrapper" style={{ maxHeight: '60vh', overflowY: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    {!historyProduct && <th>Sản phẩm</th>}
                    <th>Loại</th>
                    <th>Số lượng</th>
                    <th>Tồn sau</th>
                    <th>Giữ chỗ sau</th>
                  </tr>
                </thead>
                <tbody>
                  {historyLoading ? (
                    <tr><td colSpan={historyProduct ? 5 : 6}>Đang tải...</td></tr>
                  ) : transactions.length === 0 ? (
                    <tr><td colSpan={historyProduct ? 5 : 6}>Chưa có lịch sử.</td></tr>
                  ) : transactions.map(t => (
                    <tr key={t.id}>
                      <td style={{ fontSize: '0.8rem' }}>{new Date(t.createdAt).toLocaleString('vi-VN')}</td>
                      {!historyProduct && <td>{t.productName}</td>}
                      <td>{stockTransactionTypeLabel[t.type] || t.type}</td>
                      <td>{t.quantity > 0 ? `+${t.quantity}` : t.quantity}</td>
                      <td>{t.quantityOnHandAfter.toLocaleString()}</td>
                      <td>{t.quantityReservedAfter.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
              <button type="button" className="btn btn-outline" onClick={() => setShowHistory(false)}>Đóng</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

const emptyOrderItem = () => ({ productId: '', quantity: 1, unitPrice: '' });

const OrderManager = ({ api, getErrorDetail }) => {
  const [orders, setOrders] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [products, setProducts] = useState([]);
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [loading, setLoading] = useState(false);

  const emptyOrderForm = { customerId: '', note: '', items: [emptyOrderItem()] };
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [orderForm, setOrderForm] = useState(emptyOrderForm);

  const [showCancelModal, setShowCancelModal] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  useEffect(() => {
    fetchOrders();
    fetchCustomers();
    fetchProducts();
  }, []);

  const fetchOrders = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/orders');
      setOrders(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách đơn hàng: ' + getErrorDetail(error));
    } finally {
      setLoading(false);
    }
  };

  const fetchCustomers = async () => {
    try {
      const res = await api.get('/api/v1/customers');
      setCustomers(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách khách hàng: ' + getErrorDetail(error));
    }
  };

  const fetchProducts = async () => {
    try {
      const res = await api.get('/api/v1/products', { params: { isActive: true } });
      setProducts(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách sản phẩm: ' + getErrorDetail(error));
    }
  };

  const activeCustomers = customers.filter(c => c.isActive);

  const openCreateOrder = () => {
    if (activeCustomers.length === 0) {
      toast.error('Vui lòng tạo Khách hàng trước khi tạo đơn hàng');
      return;
    }
    if (products.length === 0) {
      toast.error('Vui lòng tạo Sản phẩm trước khi tạo đơn hàng');
      return;
    }
    setOrderForm({
      customerId: activeCustomers[0]?.id || '',
      note: '',
      items: [{ ...emptyOrderItem(), productId: products[0]?.id || '', unitPrice: products[0]?.price ?? '' }]
    });
    setShowCreateModal(true);
  };

  const updateItem = (index, field, value) => {
    const items = [...orderForm.items];
    items[index] = { ...items[index], [field]: value };

    if (field === 'productId') {
      const product = products.find(p => p.id === value);
      if (product) items[index].unitPrice = product.price;
    }

    setOrderForm({ ...orderForm, items });
  };

  const addItemRow = () => {
    setOrderForm({
      ...orderForm,
      items: [...orderForm.items, { ...emptyOrderItem(), productId: products[0]?.id || '', unitPrice: products[0]?.price ?? '' }]
    });
  };

  const removeItemRow = (index) => {
    setOrderForm({ ...orderForm, items: orderForm.items.filter((_, i) => i !== index) });
  };

  const orderFormTotal = orderForm.items.reduce(
    (sum, i) => sum + (Number(i.quantity) || 0) * (Number(i.unitPrice) || 0), 0
  );

  const handleCreateOrder = async (e) => {
    e.preventDefault();
    const invalid = orderForm.items.some(
      i => !i.productId || Number(i.quantity) <= 0 || Number(i.unitPrice) < 0
    );
    if (invalid) {
      toast.error('Vui lòng chọn sản phẩm và nhập đầy đủ thông tin hợp lệ');
      return;
    }
    try {
      await api.post('/api/v1/orders', {
        customerId: orderForm.customerId,
        note: orderForm.note || null,
        items: orderForm.items.map(i => ({
          productId: i.productId,
          quantity: Number(i.quantity),
          unitPrice: Number(i.unitPrice)
        }))
      });
      toast.success('Tạo đơn hàng thành công');
      setShowCreateModal(false);
      fetchOrders();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const handleConfirmOrder = async (order) => {
    try {
      await api.post(`/api/v1/orders/${order.id}/confirm`);
      toast.success('Đã xác nhận đơn hàng');
      setSelectedOrder(null);
      fetchOrders();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const openCancelOrder = (order) => {
    setSelectedOrder(order);
    setCancelReason('');
    setShowCancelModal(true);
  };

  const handleCancelOrder = async (e) => {
    e.preventDefault();
    try {
      await api.post(`/api/v1/orders/${selectedOrder.id}/cancel`, { reason: cancelReason });
      toast.success('Đã hủy đơn hàng');
      setShowCancelModal(false);
      setSelectedOrder(null);
      fetchOrders();
    } catch (error) {
      toast.error(getErrorDetail(error));
    }
  };

  const getStatusBadge = (status) => {
    if (status === 1) return <span className="status-badge status-paid">Đã xác nhận</span>;
    if (status === 2) return <span className="status-badge" style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }}>Đã hủy</span>;
    return <span className="status-badge status-pending">Chờ xử lý</span>;
  };

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginBottom: '1rem' }}>
        <button className="btn btn-outline" onClick={fetchOrders} disabled={loading}>
          <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Làm mới
        </button>
        <button className="btn btn-primary" onClick={openCreateOrder}>
          <Plus size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
          Tạo đơn hàng
        </button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: selectedOrder ? '1fr 1fr' : '1fr', gap: '2rem' }}>
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr>
                <th>Mã đơn</th>
                <th>Khách hàng</th>
                <th>Ngày đặt</th>
                <th>Tổng tiền</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {orders.map(o => (
                <tr key={o.id}
                    style={{ cursor: 'pointer', background: selectedOrder?.id === o.id ? 'rgba(56, 189, 248, 0.05)' : 'transparent' }}
                    onClick={() => setSelectedOrder(o)}>
                  <td style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{o.orderNumber}</td>
                  <td style={{ fontWeight: 600 }}>{o.customerName}</td>
                  <td>{new Date(o.orderDate).toLocaleDateString()}</td>
                  <td>{o.totalAmount.toLocaleString()}</td>
                  <td>{getStatusBadge(o.status)}</td>
                  <td><ChevronRight size={16} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {selectedOrder && (
          <div style={{ borderLeft: '1px solid rgba(255, 255, 255, 0.1)', paddingLeft: '2rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem' }}>
              <h3 style={{ color: '#94a3b8' }}>Đơn hàng {selectedOrder.orderNumber}</h3>
              <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={() => setSelectedOrder(null)}>
                <X size={14} />
              </button>
            </div>
            <div style={{ marginBottom: '1rem' }}>{getStatusBadge(selectedOrder.status)}</div>
            <div style={{ fontSize: '0.875rem', color: '#94a3b8', marginBottom: '1rem', display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
              <div>Khách hàng: <strong style={{ color: '#e2e8f0' }}>{selectedOrder.customerName}</strong></div>
              {selectedOrder.note && <div>Ghi chú: {selectedOrder.note}</div>}
              {selectedOrder.cancelReason && <div style={{ color: '#ef4444' }}>Lý do hủy: {selectedOrder.cancelReason}</div>}
            </div>

            <table className="table">
              <thead>
                <tr><th>Sản phẩm</th><th>SL</th><th>Đơn giá</th><th>Thành tiền</th></tr>
              </thead>
              <tbody>
                {selectedOrder.items.map(item => (
                  <tr key={item.id}>
                    <td>{item.productName}</td>
                    <td>{item.quantity}</td>
                    <td>{item.unitPrice.toLocaleString()}</td>
                    <td>{item.lineTotal.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div style={{ textAlign: 'right', fontWeight: 700, marginTop: '0.75rem' }}>
              Tổng cộng: {selectedOrder.totalAmount.toLocaleString()}
            </div>

            {selectedOrder.status === 0 && (
              <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1.5rem' }}>
                <button className="btn btn-primary" onClick={() => handleConfirmOrder(selectedOrder)}>Xác nhận</button>
                <button className="btn btn-outline" onClick={() => openCancelOrder(selectedOrder)}>Hủy đơn</button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Create Order Modal */}
      {showCreateModal && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ maxWidth: '640px' }}>
            <h2 style={{ marginBottom: '1.5rem' }}>Tạo đơn hàng mới</h2>
            <form onSubmit={handleCreateOrder}>
              <div className="form-group">
                <label className="form-label">Khách hàng</label>
                <select
                  className="form-input"
                  value={orderForm.customerId}
                  onChange={e => setOrderForm({ ...orderForm, customerId: e.target.value })}
                  required
                >
                  <option value="" disabled>Chọn khách hàng</option>
                  {activeCustomers.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label className="form-label">Ghi chú</label>
                <input
                  type="text"
                  className="form-input"
                  value={orderForm.note}
                  onChange={e => setOrderForm({ ...orderForm, note: e.target.value })}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Sản phẩm</label>
                {orderForm.items.map((item, idx) => (
                  <div key={idx} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.5rem', alignItems: 'center' }}>
                    <select
                      className="form-input"
                      style={{ flex: 3 }}
                      value={item.productId}
                      onChange={e => updateItem(idx, 'productId', e.target.value)}
                      required
                    >
                      <option value="" disabled>Chọn sản phẩm</option>
                      {products.map(p => (
                        <option key={p.id} value={p.id}>{p.name} ({p.code})</option>
                      ))}
                    </select>
                    <input
                      type="number"
                      className="form-input"
                      placeholder="SL"
                      style={{ flex: 1 }}
                      min="1"
                      value={item.quantity}
                      onChange={e => updateItem(idx, 'quantity', e.target.value)}
                      required
                    />
                    <input
                      type="number"
                      className="form-input"
                      placeholder="Đơn giá"
                      style={{ flex: 2 }}
                      min="0"
                      step="0.01"
                      value={item.unitPrice}
                      onChange={e => updateItem(idx, 'unitPrice', e.target.value)}
                      required
                    />
                    <button
                      type="button"
                      className="btn btn-outline"
                      style={{ padding: '4px 8px' }}
                      onClick={() => removeItemRow(idx)}
                      disabled={orderForm.items.length === 1}
                    >
                      <X size={14} />
                    </button>
                  </div>
                ))}
                <button type="button" className="btn btn-outline" onClick={addItemRow} style={{ marginTop: '0.5rem' }}>
                  <Plus size={14} style={{ marginRight: '4px', verticalAlign: 'middle' }} />
                  Thêm sản phẩm
                </button>
              </div>

              <div style={{ textAlign: 'right', fontWeight: 700, margin: '1rem 0' }}>
                Tổng cộng: {orderFormTotal.toLocaleString()}
              </div>

              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowCreateModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Tạo đơn hàng</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Cancel Order Modal */}
      {showCancelModal && selectedOrder && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Hủy đơn hàng {selectedOrder.orderNumber}</h2>
            <form onSubmit={handleCancelOrder}>
              <div className="form-group">
                <label className="form-label">Lý do hủy</label>
                <textarea
                  className="form-input"
                  style={{ minHeight: '80px', paddingTop: '0.5rem' }}
                  value={cancelReason}
                  onChange={e => setCancelReason(e.target.value)}
                  required
                />
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowCancelModal(false)}>Đóng</button>
                <button type="submit" className="btn btn-primary">Xác nhận hủy</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

function App() {
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [user, setUser] = useState(JSON.parse(localStorage.getItem('user') || 'null'));
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

  const { t } = useTranslation(['common', 'errors']);

  const getErrorDetail = (error) => {
    if (error.response) {
      const data = error.response.data;
      
      // Handle standardized error response
      if (data.code) {
        return t(`errors:${data.code}`, data.params || {});
      }

      if (typeof data === 'string') return data;
      if (data.errors) {
        if (Array.isArray(data.errors)) return data.errors.join(', ');
        return Object.values(data.errors).flat().join(', ');
      }
      return data.message || data.error || data.title || JSON.stringify(data);
    }
    return error.message || t('errors:COMMON.INTERNAL_ERROR');
  };

  const changeLanguage = async (lng) => {
    await i18n.changeLanguage(lng);
    if (user && token) {
      try {
        await api.put(`/api/v1/users/${user.id}/language`, JSON.stringify(lng), {
            headers: { 'Content-Type': 'application/json' }
        });
      } catch (err) {
        console.error('Failed to sync language preference to backend', err);
      }
    }
  };

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
      const { accessToken, id, username, avatarUrl, roles, permissions } = res.data;
      
      const userData = { id, username, avatarUrl, roles, permissions };
      setToken(accessToken);
      setUser(userData);
      localStorage.setItem('token', accessToken);
      localStorage.setItem('user', JSON.stringify(userData));
      toast.success('Chào mừng quay trở lại, ' + username + '!');
    } catch (error) {
      setAuthError('Tên đăng nhập hoặc mật khẩu không đúng');
      toast.error('Đăng nhập thất bại');
    }
  };

  const handleLogout = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('token');
    localStorage.removeItem('user');
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
      toast.error('Lỗi khi tạo hóa đơn: ' + getErrorDetail(error));
    }
  };

  const handlePay = async (invoiceId, amount) => {
    const toastId = toast.loading('Đang xử lý thanh toán (Async)...');
    try {
      // 1. Submit Request with Idempotency Key
      const idempotencyKey = `pay_${invoiceId}_${new Date().getTime()}`;
      
      const res = await api.post('/api/v1/payment/pay', {
        invoiceId,
        amount,
        paymentMethod: 'CreditCard'
      }, {
        headers: {
          'X-Idempotency-Key': idempotencyKey
        }
      });
      
      const { paymentId } = res.data;

      // 2. Thiết lập SignalR
      const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${GATEWAY_URL}/hubs/payment?access_token=${token}`)
        .withAutomaticReconnect()
        .build();

      let isResolved = false;

      await new Promise(async (resolve, reject) => {
        // Lắng nghe Push Event (Real-time UX)
        connection.on("PaymentStatusUpdated", (event) => {
          if (isResolved) return;
          isResolved = true;
          connection.stop();
          if (event.status === 'Completed') resolve(event);
          else reject(new Error(event.failureReason || 'Thanh toán thất bại'));
        });

        try {
          await connection.start();
          await connection.invoke("WatchPayment", paymentId);
        } catch (err) {
          console.warn("SignalR connection failed, falling back to polling exclusively", err);
        }

        // 3. Fallback: Polling Loop (Correctness Guarantee)
        const startTime = Date.now();
        const maxTimeout = 60000; // 60 seconds

        const pollStatus = async () => {
          while (!isResolved && Date.now() - startTime < maxTimeout) {
            try {
              const statusRes = await api.get(`/api/v1/payment/${paymentId}`);
              const statusData = statusRes.data;
              
              if (statusData.status === 'Completed') {
                isResolved = true;
                connection.stop();
                return resolve(statusData);
              }
              if (statusData.status === 'Failed') {
                isResolved = true;
                connection.stop();
                return reject(new Error(statusData.failureReason));
              }
              
              // Exponential backoff wait
              await new Promise(r => setTimeout(r, (statusData.retryAfter || 2) * 1000));
            } catch (pollErr) {
              console.error("Polling error:", pollErr);
              await new Promise(r => setTimeout(r, 2000));
            }
          }
          
          if (!isResolved) {
            connection.stop();
            reject(new Error("Timeout: Giao dịch đang xử lý quá lâu, vui lòng kiểm tra lại sau."));
          }
        };

        pollStatus();
      });

      fetchData();
      toast.success('Thanh toán hoàn tất!', { id: toastId });
    } catch (error) {
      console.error('Error processing payment:', error);
      toast.error(error.message || ('Thanh toán thất bại: ' + getErrorDetail(error)), { id: toastId });
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
        <div style={{ padding: '2rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '2.5rem' }}>
            <div style={{ background: '#2563eb', padding: '0.75rem', borderRadius: '1rem' }}>
              <LayoutDashboard color="white" size={24} />
            </div>
            <div>
              <h1 style={{ fontSize: '1.25rem', fontWeight: 800, letterSpacing: '-0.025em' }}>{t('common:app_name')}</h1>
              <div style={{ fontSize: '0.7rem', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Enterprise Edition</div>
            </div>
          </div>

          {/* Language Switcher */}
          <div style={{ marginBottom: '2rem', display: 'flex', gap: '0.5rem', background: 'rgba(255,255,255,0.05)', padding: '4px', borderRadius: '8px' }}>
            <button 
              onClick={() => changeLanguage('vi')}
              style={{ 
                flex: 1, padding: '4px', border: 'none', borderRadius: '6px', cursor: 'pointer',
                background: i18n.language === 'vi' ? '#2563eb' : 'transparent',
                color: i18n.language === 'vi' ? 'white' : '#94a3b8',
                fontSize: '0.75rem', fontWeight: 600
              }}
            >
              TIẾNG VIỆT
            </button>
            <button 
              onClick={() => changeLanguage('en')}
              style={{ 
                flex: 1, padding: '4px', border: 'none', borderRadius: '6px', cursor: 'pointer',
                background: i18n.language === 'en' ? '#2563eb' : 'transparent',
                color: i18n.language === 'en' ? 'white' : '#94a3b8',
                fontSize: '0.75rem', fontWeight: 600
              }}
            >
              ENGLISH
            </button>
          </div>

          <nav>
            <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
              <LayoutDashboard size={20} />
              {t('common:dashboard')}
            </div>
            <div className={`nav-item ${activeTab === 'invoices' ? 'active' : ''}`} onClick={() => setActiveTab('invoices')}>
              <FileText size={20} />
              {t('common:invoices')}
            </div>
            <div className={`nav-item ${activeTab === 'customers' ? 'active' : ''}`} onClick={() => setActiveTab('customers')}>
              <Users size={20} />
              {t('common:customers')}
            </div>
            <div className={`nav-item ${activeTab === 'orders' ? 'active' : ''}`} onClick={() => setActiveTab('orders')}>
              <ShoppingCart size={20} />
              {t('common:orders')}
            </div>
            <div className={`nav-item ${activeTab === 'products' ? 'active' : ''}`} onClick={() => setActiveTab('products')}>
              <Package size={20} />
              {t('common:products')}
            </div>
            <div className={`nav-item ${activeTab === 'inventory' ? 'active' : ''}`} onClick={() => setActiveTab('inventory')}>
              <Warehouse size={20} />
              {t('common:inventory')}
            </div>
            <div className={`nav-item ${activeTab === 'orchestration' ? 'active' : ''}`} onClick={() => setActiveTab('orchestration')}>
              <Activity size={20} />
              {t('common:orchestration')}
            </div>
            <div className={`nav-item ${activeTab === 'identity' ? 'active' : ''}`} onClick={() => setActiveTab('identity')}>
              <ShieldCheck size={20} />
              {t('common:roles')}
            </div>
            <div className={`nav-item ${activeTab === 'audit' ? 'active' : ''}`} onClick={() => setActiveTab('audit')}>
              <Search size={20} />
              {t('common:audit')}
            </div>
          </nav>
        </div>
        <div className="nav-item logout" onClick={handleLogout} style={{ borderTop: '1px solid #334155', padding: '1.5rem', marginTop: 'auto', color: '#94a3b8' }}>
          <LogOut size={20} /> {t('common:logout')}
        </div>
      </aside>

      {/* Main Content */}
      <main className="main-content">
        <header className="header">
          <h1 className="title">
            {activeTab === 'dashboard' && t('common:dashboard')}
            {activeTab === 'invoices' && t('common:invoices')}
            {activeTab === 'customers' && t('common:customers')}
            {activeTab === 'orders' && t('common:orders')}
            {activeTab === 'products' && t('common:products')}
            {activeTab === 'inventory' && t('common:inventory')}
            {activeTab === 'orchestration' && t('common:orchestration')}
            {activeTab === 'identity' && t('common:roles')}
            {activeTab === 'audit' && t('common:audit')}
          </h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem' }}>
            {activeTab === 'invoices' && (
              <button className="btn btn-primary" onClick={() => setShowModal(true)}>
                <Plus size={20} style={{ verticalAlign: 'middle', marginRight: '5px' }} />
                Tạo hóa đơn mới
              </button>
            )}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.5rem 1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '2rem' }}>
              <div style={{ width: '32px', height: '32px', borderRadius: '50%', overflow: 'hidden', background: '#334155', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                {user?.avatarUrl ? (
                  <img src={user.avatarUrl} alt="Avatar" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                ) : (
                  <UserIcon size={18} color="#94a3b8" />
                )}
              </div>
              <span style={{ fontWeight: 500, fontSize: '0.875rem' }}>{user?.username}</span>
            </div>
          </div>
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
        ) : activeTab === 'customers' ? (
          <CustomerManager api={api} getErrorDetail={getErrorDetail} />
        ) : activeTab === 'orders' ? (
          <OrderManager api={api} getErrorDetail={getErrorDetail} />
        ) : activeTab === 'products' ? (
          <ProductManager api={api} getErrorDetail={getErrorDetail} />
        ) : activeTab === 'inventory' ? (
          <InventoryManager api={api} getErrorDetail={getErrorDetail} />
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

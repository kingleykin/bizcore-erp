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
  Contact,
  Mail,
  Phone,
  MapPin
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
                  onChange={e => setUserForm({ ...userForm, username: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Email</label>
                <input
                  type="email"
                  className="form-input"
                  value={userForm.email}
                  onChange={e => setUserForm({ ...userForm, email: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mật khẩu</label>
                <input
                  type="password"
                  className="form-input"
                  value={userForm.password}
                  onChange={e => setUserForm({ ...userForm, password: e.target.value })}
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
                  onChange={e => setRoleForm({ ...roleForm, name: e.target.value })}
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
                  onChange={e => setRoleForm({ ...roleForm, description: e.target.value })}
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

// ── Customer Manager Component ────────────────────────────────────────────────
const CustomerManager = ({ api }) => {
  const [activeSubTab, setActiveSubTab] = useState('customers');
  const [customers, setCustomers] = useState([]);
  const [customerGroups, setCustomerGroups] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showGroupModal, setShowGroupModal] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState(null);
  const [selectedGroup, setSelectedGroup] = useState(null);
  const [searchTerm, setSearchTerm] = useState('');
  
  const [customerForm, setCustomerForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    address: '',
    customerGroupId: ''
  });

  const [editCustomerForm, setEditCustomerForm] = useState({
    firstName: '',
    lastName: '',
    phone: '',
    address: '',
    customerGroupId: ''
  });

  const [groupForm, setGroupForm] = useState({
    nameCustomerGroup: '',
    code: '',
    description: '',
    status: 0
  });

  useEffect(() => {
    if (activeSubTab === 'customers') {
      fetchCustomers();
      if (customerGroups.length === 0) fetchCustomerGroups();
    } else {
      fetchCustomerGroups();
    }
  }, [activeSubTab]);

  const fetchCustomers = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/customer');
      setCustomers(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách khách hàng');
    } finally {
      setLoading(false);
    }
  };

  const fetchCustomerGroups = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/v1/customergroup');
      setCustomerGroups(res.data);
    } catch (error) {
      toast.error('Lỗi khi tải danh sách nhóm khách hàng');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateCustomer = async (e) => {
    e.preventDefault();
    try {
      const payload = { ...customerForm };
      if (!payload.customerGroupId) payload.customerGroupId = null;

      await api.post('/api/v1/customer', payload);
      toast.success('Tạo khách hàng thành công!');
      setShowCreateModal(false);
      setCustomerForm({ firstName: '', lastName: '', email: '', phone: '', address: '', customerGroupId: '' });
      fetchCustomers();
    } catch (error) {
      const data = error.response?.data;
      const msg = typeof data === 'string' ? data : data?.message || data?.title || JSON.stringify(data);
      toast.error('Lỗi khi tạo khách hàng: ' + msg);
    }
  };

  const openEditCustomerModal = (cust) => {
    setEditCustomerForm({
      firstName: cust.firstName || '',
      lastName: cust.lastName || '',
      phone: cust.phone || '',
      address: cust.address || '',
      customerGroupId: cust.customerGroupId || ''
    });
    setShowEditModal(true);
  };

  const handleUpdateCustomer = async (e) => {
    e.preventDefault();
    try {
      const payload = { ...editCustomerForm };
      if (!payload.customerGroupId) payload.customerGroupId = null;

      await api.put(`/api/v1/customer/${selectedCustomer.id}`, payload);
      toast.success('Cập nhật khách hàng thành công!');
      setShowEditModal(false);
      // refresh and update selectedCustomer
      const res = await api.get('/api/v1/customer');
      setCustomers(res.data);
      const updated = res.data.find(c => c.id === selectedCustomer.id);
      if (updated) setSelectedCustomer(updated);
    } catch (error) {
      const data = error.response?.data;
      const msg = typeof data === 'string' ? data : data?.message || data?.title || JSON.stringify(data);
      toast.error('Lỗi khi cập nhật khách hàng: ' + msg);
    }
  };

  const handleSaveGroup = async (e) => {
    e.preventDefault();
    try {
      if (selectedGroup) {
        await api.put(`/api/v1/customergroup/${selectedGroup.id}`, { id: selectedGroup.id, ...groupForm });
        toast.success('Cập nhật nhóm khách hàng thành công!');
      } else {
        await api.post('/api/v1/customergroup', groupForm);
        toast.success('Tạo nhóm khách hàng thành công!');
      }
      setShowGroupModal(false);
      setSelectedGroup(null);
      setGroupForm({ nameCustomerGroup: '', code: '', description: '', status: 0 });
      fetchCustomerGroups();
    } catch (error) {
      const data = error.response?.data;
      const msg = typeof data === 'string' ? data : data?.message || data?.title || JSON.stringify(data);
      toast.error('Lỗi khi lưu nhóm khách hàng: ' + msg);
    }
  };

  const handleDeleteGroup = async (id) => {
    if (!window.confirm('Bạn có chắc chắn muốn xóa nhóm này không?')) return;
    try {
      await api.delete(`/api/v1/customergroup/${id}`);
      toast.success('Xóa nhóm thành công!');
      fetchCustomerGroups();
    } catch (error) {
      toast.error('Lỗi khi xóa nhóm khách hàng');
    }
  };

  const openEditGroupModal = (group) => {
    setSelectedGroup(group);
    setGroupForm({
      nameCustomerGroup: group.nameCustomerGroup,
      code: group.code,
      description: group.description || '',
      status: group.status
    });
    setShowGroupModal(true);
  };

  const getStatusInfo = (status) => {
    switch (status) {
      case 0: return { label: 'Hoạt động', color: '#22c55e' };
      case 1: return { label: 'Đã tạo TK', color: '#38bdf8' };
      case 2: return { label: 'Đã khóa', color: '#ef4444' };
      default: return { label: 'N/A', color: '#94a3b8' };
    }
  };

  const filteredCustomers = customers.filter(c => {
    const term = searchTerm.toLowerCase();
    return (
      c.firstName?.toLowerCase().includes(term) ||
      c.lastName?.toLowerCase().includes(term) ||
      c.email?.toLowerCase().includes(term) ||
      c.phone?.includes(term)
    );
  });

  return (
    <>
      <div className="card">
        <div className="tab-header" style={{ marginBottom: '1.5rem' }}>
          <div className={`tab-btn ${activeSubTab === 'customers' ? 'active' : ''}`} onClick={() => setActiveSubTab('customers')}>
            <Users size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
            Khách hàng
          </div>
          <div className={`tab-btn ${activeSubTab === 'groups' ? 'active' : ''}`} onClick={() => setActiveSubTab('groups')}>
            <LayoutDashboard size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
            Nhóm khách hàng
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
          <h2 style={{ fontSize: '1.25rem' }}>{activeSubTab === 'customers' ? 'Quản lý khách hàng' : 'Nhóm khách hàng'}</h2>
          <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
            {activeSubTab === 'customers' && (
              <div style={{ position: 'relative' }}>
                <Search size={16} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: '#64748b' }} />
                <input
                  type="text"
                  placeholder="Tìm kiếm khách hàng..."
                  value={searchTerm}
                  onChange={e => setSearchTerm(e.target.value)}
                  className="form-input"
                  style={{ paddingLeft: '36px', width: '260px', margin: 0 }}
                />
              </div>
            )}
            <button className="btn btn-outline" onClick={activeSubTab === 'customers' ? fetchCustomers : fetchCustomerGroups} disabled={loading}>
              <RefreshCcw size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
              Làm mới
            </button>
            <button className="btn btn-primary" onClick={() => {
              if (activeSubTab === 'customers') {
                setShowCreateModal(true);
              } else {
                setSelectedGroup(null);
                setGroupForm({ nameCustomerGroup: '', code: '', description: '', status: 0 });
                setShowGroupModal(true);
              }
            }}>
              <Plus size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
              {activeSubTab === 'customers' ? 'Thêm khách hàng' : 'Thêm nhóm KH'}
            </button>
          </div>
        </div>

        {/* Stats Row */}
        {activeSubTab === 'customers' && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
            <div style={{ background: 'rgba(34, 197, 94, 0.08)', padding: '1rem 1.25rem', borderRadius: '0.75rem', border: '1px solid rgba(34, 197, 94, 0.15)' }}>
              <div style={{ fontSize: '0.75rem', color: '#94a3b8', marginBottom: '0.25rem' }}>Tổng khách hàng</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>{customers.length}</div>
            </div>
            <div style={{ background: 'rgba(56, 189, 248, 0.08)', padding: '1rem 1.25rem', borderRadius: '0.75rem', border: '1px solid rgba(56, 189, 248, 0.15)' }}>
              <div style={{ fontSize: '0.75rem', color: '#94a3b8', marginBottom: '0.25rem' }}>Đang hoạt động</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#38bdf8' }}>{customers.filter(c => c.status === 0).length}</div>
            </div>
            <div style={{ background: 'rgba(239, 68, 68, 0.08)', padding: '1rem 1.25rem', borderRadius: '0.75rem', border: '1px solid rgba(239, 68, 68, 0.15)' }}>
              <div style={{ fontSize: '0.75rem', color: '#94a3b8', marginBottom: '0.25rem' }}>Đã khóa</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#ef4444' }}>{customers.filter(c => c.status === 2).length}</div>
            </div>
          </div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: (activeSubTab === 'customers' && selectedCustomer) ? '1fr 1fr' : '1fr', gap: '2rem' }}>
          <div className="table-wrapper">
            {activeSubTab === 'customers' ? (
              <table className="table">
                <thead>
                  <tr>
                    <th>Họ tên</th>
                    <th>Email</th>
                    <th>SĐT</th>
                    <th>Trạng thái</th>
                    <th>Ngày tạo</th>
                    <th>Điểm tích lũy</th>
                    <th>Số tiền trong tài khoản</th>
                    <th>Số tiền tổng hóa đơn</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr><td colSpan="9" style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Đang tải...</td></tr>
                  ) : filteredCustomers.length === 0 ? (
                    <tr><td colSpan="9" style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Không có khách hàng nào</td></tr>
                  ) : filteredCustomers.map(cust => {
                    const statusInfo = getStatusInfo(cust.status);
                    return (
                      <tr key={cust.id}
                        style={{ cursor: 'pointer', background: selectedCustomer?.id === cust.id ? 'rgba(56, 189, 248, 0.05)' : 'transparent' }}
                        onClick={() => setSelectedCustomer(cust)}>
                        <td style={{ fontWeight: 500 }}>{cust.firstName} {cust.lastName}</td>
                        <td style={{ fontSize: '0.875rem' }}>{cust.email}</td>
                        <td style={{ fontSize: '0.875rem' }}>{cust.phone}</td>
                        <td>
                          <span className="status-badge" style={{ background: `${statusInfo.color}20`, color: statusInfo.color }}>
                            {statusInfo.label}
                          </span>
                        </td>
                        <td style={{ fontSize: '0.875rem' }}>{new Date(cust.createdAt).toLocaleDateString()}</td>
                        <td style={{ fontSize: '0.875rem' }}>{cust.customerPoint}</td>
                        <td style={{ fontSize: '0.875rem' }}>{cust.soTienTrongTaiKhoan}</td>
                        <td style={{ fontSize: '0.875rem' }}>{cust.soTienTongHoaDon}</td>
                        <td><ChevronRight size={16} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Tên nhóm</th>
                    <th>Mã nhóm</th>
                    <th>Mô tả</th>
                    <th>Trạng thái</th>
                    <th>Ngày tạo</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr><td colSpan="6" style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Đang tải...</td></tr>
                  ) : customerGroups.length === 0 ? (
                    <tr><td colSpan="6" style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Không có nhóm khách hàng nào</td></tr>
                  ) : customerGroups.map(group => {
                    const isBlocked = group.status === 1;
                    const statusColor = isBlocked ? '#ef4444' : '#22c55e';
                    return (
                      <tr key={group.id}>
                        <td style={{ fontWeight: 500 }}>{group.nameCustomerGroup}</td>
                        <td style={{ fontSize: '0.875rem', fontFamily: 'monospace' }}>{group.code}</td>
                        <td style={{ fontSize: '0.875rem', color: '#94a3b8' }}>{group.description || 'Không có mô tả'}</td>
                        <td>
                          <span className="status-badge" style={{ background: `${statusColor}20`, color: statusColor }}>
                            {isBlocked ? 'Đã khóa' : 'Hoạt động'}
                          </span>
                        </td>
                        <td style={{ fontSize: '0.875rem' }}>{new Date(group.createdAt).toLocaleDateString()}</td>
                        <td>
                          <div style={{ display: 'flex', gap: '0.5rem' }}>
                            <button className="btn btn-outline" style={{ padding: '4px 8px' }} onClick={(e) => { e.stopPropagation(); openEditGroupModal(group); }}>
                              Sửa
                            </button>
                            <button className="btn btn-outline" style={{ padding: '4px 8px', borderColor: 'rgba(239, 68, 68, 0.5)', color: '#ef4444' }} onClick={(e) => { e.stopPropagation(); handleDeleteGroup(group.id); }}>
                              Xóa
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>

          {selectedCustomer && (
            <div style={{ borderLeft: '1px solid rgba(255, 255, 255, 0.1)', paddingLeft: '2rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <h3 style={{ marginBottom: '1.5rem', color: '#94a3b8' }}>Chi tiết khách hàng</h3>
                <button className="btn btn-outline" style={{ padding: '4px' }} onClick={() => setSelectedCustomer(null)}>×</button>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1.5rem' }}>
                <div style={{ width: '56px', height: '56px', borderRadius: '50%', background: 'linear-gradient(135deg, #2563eb, #38bdf8)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.25rem', fontWeight: 700, color: 'white' }}>
                  {selectedCustomer.firstName?.charAt(0)}{selectedCustomer.lastName?.charAt(0)}
                </div>
                <div>
                  <div style={{ fontSize: '1.125rem', fontWeight: 600 }}>{selectedCustomer.firstName} {selectedCustomer.lastName}</div>
                  <div style={{ fontSize: '0.75rem', color: '#64748b', fontFamily: 'monospace' }}>ID: {selectedCustomer.id?.substring(0, 8)}...</div>
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.03)', borderRadius: '0.5rem' }}>
                  <Mail size={16} color="#64748b" />
                  <div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b', textTransform: 'uppercase' }}>Email</div>
                    <div style={{ fontSize: '0.875rem' }}>{selectedCustomer.email}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.03)', borderRadius: '0.5rem' }}>
                  <Phone size={16} color="#64748b" />
                  <div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b', textTransform: 'uppercase' }}>Số điện thoại</div>
                    <div style={{ fontSize: '0.875rem' }}>{selectedCustomer.phone}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.03)', borderRadius: '0.5rem' }}>
                  <MapPin size={16} color="#64748b" />
                  <div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b', textTransform: 'uppercase' }}>Địa chỉ</div>
                    <div style={{ fontSize: '0.875rem' }}>{selectedCustomer.address || 'Chưa cập nhật'}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.03)', borderRadius: '0.5rem' }}>
                  <Clock size={16} color="#64748b" />
                  <div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b', textTransform: 'uppercase' }}>Ngày tạo</div>
                    <div style={{ fontSize: '0.875rem' }}>{new Date(selectedCustomer.createdAt).toLocaleString()}</div>
                  </div>
                </div>
              {/* Group info */}
              {selectedCustomer.customerGroupId && (() => {
                const grp = customerGroups.find(g => g.id === selectedCustomer.customerGroupId);
                return grp ? (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.03)', borderRadius: '0.5rem' }}>
                    <LayoutDashboard size={16} color="#64748b" />
                    <div>
                      <div style={{ fontSize: '0.7rem', color: '#64748b', textTransform: 'uppercase' }}>Nhóm KH</div>
                      <div style={{ fontSize: '0.875rem' }}>{grp.nameCustomerGroup}</div>
                    </div>
                  </div>
                ) : null;
              })()}
            </div>

              {(() => {
                const si = getStatusInfo(selectedCustomer.status);
                return (
                  <div style={{ marginTop: '1.5rem', padding: '0.75rem 1rem', background: `${si.color}10`, borderRadius: '0.5rem', border: `1px solid ${si.color}30` }}>
                    <span style={{ fontSize: '0.75rem', color: si.color, fontWeight: 600 }}>Trạng thái: {si.label}</span>
                  </div>
                );
              })()}

              <button
                className="btn btn-primary"
                style={{ marginTop: '1rem', width: '100%' }}
                onClick={() => openEditCustomerModal(selectedCustomer)}
              >
                Sửa thông tin khách hàng
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Modal Tạo Khách Hàng */}
      {showCreateModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Thêm khách hàng mới</h2>
            <form onSubmit={handleCreateCustomer}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                <div className="form-group">
                  <label className="form-label">Họ</label>
                  <input
                    type="text"
                    className="form-input"
                    value={customerForm.firstName}
                    onChange={e => setCustomerForm({ ...customerForm, firstName: e.target.value })}
                    placeholder="Nguyễn"
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Tên</label>
                  <input
                    type="text"
                    className="form-input"
                    value={customerForm.lastName}
                    onChange={e => setCustomerForm({ ...customerForm, lastName: e.target.value })}
                    placeholder="Văn A"
                    required
                  />
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Email</label>
                <input
                  type="email"
                  className="form-input"
                  value={customerForm.email}
                  onChange={e => setCustomerForm({ ...customerForm, email: e.target.value })}
                  placeholder="email@example.com"
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Số điện thoại</label>
                <input
                  type="tel"
                  className="form-input"
                  value={customerForm.phone}
                  onChange={e => setCustomerForm({ ...customerForm, phone: e.target.value })}
                  placeholder="0901234567"
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Địa chỉ</label>
                <input
                  type="text"
                  className="form-input"
                  value={customerForm.address}
                  onChange={e => setCustomerForm({ ...customerForm, address: e.target.value })}
                  placeholder="123 Đường ABC, Quận 1, TP.HCM"
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Nhóm khách hàng (Tùy chọn)</label>
                <select
                  className="form-input"
                  value={customerForm.customerGroupId}
                  onChange={e => setCustomerForm({ ...customerForm, customerGroupId: e.target.value })}
                  style={{ background: '#0f172a' }}
                >
                  <option value="">-- Chọn nhóm --</option>
                  {customerGroups.map(group => (
                    <option key={group.id} value={group.id}>{group.nameCustomerGroup}</option>
                  ))}
                </select>
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowCreateModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Xác nhận tạo</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Sửa Khách Hàng */}
      {showEditModal && selectedCustomer && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>Sửa khách hàng: {selectedCustomer.firstName} {selectedCustomer.lastName}</h2>
            <form onSubmit={handleUpdateCustomer}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                <div className="form-group">
                  <label className="form-label">Họ</label>
                  <input
                    type="text"
                    className="form-input"
                    value={editCustomerForm.firstName}
                    onChange={e => setEditCustomerForm({ ...editCustomerForm, firstName: e.target.value })}
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Tên</label>
                  <input
                    type="text"
                    className="form-input"
                    value={editCustomerForm.lastName}
                    onChange={e => setEditCustomerForm({ ...editCustomerForm, lastName: e.target.value })}
                    required
                  />
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Số điện thoại</label>
                <input
                  type="tel"
                  className="form-input"
                  value={editCustomerForm.phone}
                  onChange={e => setEditCustomerForm({ ...editCustomerForm, phone: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Địa chỉ</label>
                <input
                  type="text"
                  className="form-input"
                  value={editCustomerForm.address}
                  onChange={e => setEditCustomerForm({ ...editCustomerForm, address: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Nhóm khách hàng</label>
                <select
                  className="form-input"
                  value={editCustomerForm.customerGroupId}
                  onChange={e => setEditCustomerForm({ ...editCustomerForm, customerGroupId: e.target.value })}
                  style={{ background: '#0f172a' }}
                >
                  <option value="">-- Không thuộc nhóm --</option>
                  {customerGroups.map(group => (
                    <option key={group.id} value={group.id}>{group.nameCustomerGroup}</option>
                  ))}
                </select>
              </div>
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowEditModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Lưu thay đổi</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Quản Lý Nhóm Khách Hàng */}
      {showGroupModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h2 style={{ marginBottom: '1.5rem' }}>{selectedGroup ? 'Sửa nhóm khách hàng' : 'Thêm nhóm mới'}</h2>
            <form onSubmit={handleSaveGroup}>
              <div className="form-group">
                <label className="form-label">Tên nhóm</label>
                <input
                  type="text"
                  className="form-input"
                  value={groupForm.nameCustomerGroup}
                  onChange={e => setGroupForm({ ...groupForm, nameCustomerGroup: e.target.value })}
                  placeholder="Khách VIP"
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mã nhóm</label>
                <input
                  type="text"
                  className="form-input"
                  value={groupForm.code}
                  onChange={e => setGroupForm({ ...groupForm, code: e.target.value })}
                  placeholder="VIP"
                  disabled={!!selectedGroup}
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
                  placeholder="Mô tả nhóm..."
                />
              </div>
              {selectedGroup && (
                <div className="form-group">
                  <label className="form-label">Trạng thái</label>
                  <select
                    className="form-input"
                    value={groupForm.status}
                    onChange={e => setGroupForm({ ...groupForm, status: parseInt(e.target.value) })}
                    style={{ background: '#0f172a' }}
                  >
                    <option value={0}>Hoạt động</option>
                    <option value={1}>Đã khóa</option>
                  </select>
                </div>
              )}
              <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                <button type="button" className="btn btn-outline" onClick={() => setShowGroupModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">{selectedGroup ? 'Lưu thay đổi' : 'Xác nhận tạo'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
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
  const [newInvoice, setNewInvoice] = useState({ customerId: '', customerName: '', amount: '' });
  const [customers, setCustomers] = useState([]);

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

  const fetchCustomersForInvoice = async () => {
    try {
      const res = await api.get('/api/v1/customer');
      setCustomers(res.data || []);
    } catch (error) {
      console.error('Error fetching customers:', error);
    }
  };

  useEffect(() => {
    if (token) {
      fetchData();
      fetchCustomersForInvoice();
      const interval = setInterval(fetchData, 5000);
      return () => clearInterval(interval);
    }
  }, [token]);

  useEffect(() => {
    if (showModal && token) {
      fetchCustomersForInvoice();
    }
  }, [showModal, token]);

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
        customerId: newInvoice.customerId,
        customerName: newInvoice.customerName.trim(),
        amount
      });
      setShowModal(false);
      setNewInvoice({ customerId: '', customerName: '', amount: '' });
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
          // We no longer resolve early on 'Completed' because we need to wait for the entire Saga
          // to finish (CustomerPointAdded) or fail (Compensating). The polling loop will handle it.
          if (event.status === 'Failed') {
            isResolved = true;
            connection.stop();
            reject(new Error(event.failureReason || 'Thanh toán thất bại'));
          }
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
              const statusRes = await api.get(`/api/v1/orchestration/flows/${invoiceId}`);
              const statusData = statusRes.data;

              const hasCompletedStep = statusData?.steps?.some(s => s.stepName === 'CustomerPointAddedObserved');
              if (statusData && (statusData.currentState === 'CustomerPointAdded' || hasCompletedStep)) {
                isResolved = true;
                connection.stop();
                return resolve(statusData);
              }
              if (statusData && (statusData.currentState === 'Failed' || statusData.currentState === 'Compensating' || statusData.currentState === 'Reverting' || statusData.currentState === 'Refunded')) {
                isResolved = true;
                connection.stop();
                // Get the last step to find the reason
                const lastStep = statusData.steps && statusData.steps.length > 0 ? statusData.steps[statusData.steps.length - 1] : null;
                return reject(new Error((lastStep && lastStep.eventData && lastStep.eventData.reason) || 'Giao dịch thất bại (Bù trừ tự động).'));
              }

              // Exponential backoff wait
              await new Promise(r => setTimeout(r, 2000));
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
                onChange={e => setLoginData({ ...loginData, username: e.target.value })}
                required
              />
            </div>
            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
              <label className="form-label">Mật khẩu</label>
              <input
                type="password"
                className="form-input"
                value={loginData.password}
                onChange={e => setLoginData({ ...loginData, password: e.target.value })}
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
            <div className={`nav-item ${activeTab === 'orchestration' ? 'active' : ''}`} onClick={() => setActiveTab('orchestration')}>
              <Activity size={20} />
              {t('common:orchestration')}
            </div>
            <div className={`nav-item ${activeTab === 'identity' ? 'active' : ''}`} onClick={() => setActiveTab('identity')}>
              <ShieldCheck size={20} />
              {t('common:roles')}
            </div>
            <div className={`nav-item ${activeTab === 'customers' ? 'active' : ''}`} onClick={() => setActiveTab('customers')}>
              <Contact size={20} />
              Khách hàng
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
            {activeTab === 'orchestration' && t('common:orchestration')}
            {activeTab === 'identity' && t('common:roles')}
            {activeTab === 'customers' && 'Khách hàng'}
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
        ) : activeTab === 'orchestration' ? (
          <OrchestrationFlow api={api} />
        ) : activeTab === 'identity' ? (
          <IdentityManager api={api} />
        ) : activeTab === 'customers' ? (
          <CustomerManager api={api} />
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
                <select
                  className="form-input"
                  value={newInvoice.customerId}
                  onChange={e => {
                    const selectedId = e.target.value;
                    const selectedCust = customers.find(c => c.id === selectedId);
                    setNewInvoice({
                      ...newInvoice,
                      customerId: selectedId,
                      customerName: selectedCust ? `${selectedCust.firstName} ${selectedCust.lastName}` : ''
                    });
                  }}
                  required
                  style={{ background: '#0f172a', color: 'white' }}
                >
                  <option value="">-- Chọn khách hàng --</option>
                  {customers.map(cust => (
                    <option key={cust.id} value={cust.id} style={{ background: '#0f172a', color: 'white' }}>
                      {cust.firstName} {cust.lastName} {cust.phone ? `(${cust.phone})` : ''}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label className="form-label">Số tiền ($)</label>
                <input
                  type="number"
                  className="form-input"
                  value={newInvoice.amount}
                  min="0.01"
                  step="0.01"
                  onChange={e => setNewInvoice({ ...newInvoice, amount: e.target.value })}
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

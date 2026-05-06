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
  LogOut
} from 'lucide-react';
import { Toaster, toast } from 'react-hot-toast';

const GATEWAY_URL = 'http://localhost:5000';

function App() {
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [authError, setAuthError] = useState('');
  const [loginData, setLoginData] = useState({ username: 'admin', password: 'password' });

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
      const res = await axios.post(`${GATEWAY_URL}/auth/login`, loginData);
      const newToken = res.data.token;
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
              Demo: admin / password
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
        </nav>
        <div className="nav-item logout" onClick={handleLogout} style={{ borderTop: '1px solid #334155', paddingTop: '1.5rem', marginTop: 'auto', color: '#94a3b8' }}>
          <CheckCircle2 size={20} /> Đăng xuất
        </div>
      </aside>

      {/* Main Content */}
      <main className="main-content">
        <header className="header">
          <h1 className="title">
            {activeTab === 'dashboard' ? 'Tổng quan hệ thống' : 'Quản lý hóa đơn'}
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
        ) : (
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
        )}
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

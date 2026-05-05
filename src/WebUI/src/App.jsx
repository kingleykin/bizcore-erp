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
  Clock 
} from 'lucide-react';

const GATEWAY_URL = 'http://localhost:5000';

function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [invoices, setInvoices] = useState([]);
  const [stats, setStats] = useState({
    totalInvoices: 0,
    totalRevenue: 0,
    paidInvoices: 0,
    pendingInvoices: 0
  });
  const [showModal, setShowModal] = useState(false);
  const [newInvoice, setNewInvoice] = useState({ customerName: '', amount: 0 });

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 5000); // Auto refresh
    return () => clearInterval(interval);
  }, []);

  const fetchData = async () => {
    try {
      const invRes = await axios.get(`${GATEWAY_URL}/invoice`);
      setInvoices(invRes.data);

      const statsRes = await axios.get(`${GATEWAY_URL}/report/summary`);
      setStats(statsRes.data);
    } catch (error) {
      console.error('Error fetching data:', error);
    }
  };

  const handleCreateInvoice = async (e) => {
    e.preventDefault();
    try {
      await axios.post(`${GATEWAY_URL}/invoice`, newInvoice);
      setShowModal(false);
      setNewInvoice({ customerName: '', amount: 0 });
      fetchData();
    } catch (error) {
      console.error('Error creating invoice:', error);
    }
  };

  const handlePay = async (invoiceId, amount) => {
    try {
      await axios.post(`${GATEWAY_URL}/payment/pay`, {
        invoiceId,
        amount
      });
      fetchData();
    } catch (error) {
      console.error('Error processing payment:', error);
    }
  };

  return (
    <div className="app-container">
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="logo">
          <LayoutDashboard size={28} />
          <span>BizCore CRM</span>
        </div>
        <nav>
          <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
            <BarChart3 size={20} /> Dashboard
          </div>
          <div className={`nav-item ${activeTab === 'invoices' ? 'active' : ''}`} onClick={() => setActiveTab('invoices')}>
            <FileText size={20} /> Hóa đơn
          </div>
        </nav>
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
                  onChange={e => setNewInvoice({...newInvoice, amount: parseFloat(e.target.value)})}
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

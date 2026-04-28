# Pharmacy Management System – Developer Documentation
## Desktop-First | LAN-Ready | Future Cloud Sync

### 1. Architecture Overview
- Desktop-first: Windows 10/11, .NET 8 WPF.
- Multi-user LAN: central PostgreSQL database on server PC; clients connect via LAN IP.
- Offline-first: no internet required for core billing/inventory.
- Optional cloud sync later: two-way sync via sync_queue table.

### 2. Technology Stack
| Component | Technology |
|-----------|------------|
| Framework | .NET 8 (C#) |
| UI | WPF (XAML) |
| Database | PostgreSQL 15+ |
| DB Driver | Npgsql |
| LAN Comm | Direct TCP/IP + connection pooling |
| Reporting | Crystal Reports / RDLC or FastReports |
| Printer | ESC/POS via raw USB/serial |

### 3. Database – PostgreSQL (Recommended)
- **Why PostgreSQL**: handles heavy data, MVCC prevents read locks, native JSON for sync, free & scalable.
- **Connection string** (client):
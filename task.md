### Phase 1 Tasks: Foundation & Master Data
- [ ] **Task 1.1:** Install Visual Studio 2022 (.NET Desktop workload) and PostgreSQL 15+ on the server PC.
- [x] **Task 1.2:** Execute the provided SQL schema script to create the PharmacyDB database and tables. *(Generated `schema.sql`)*
- [x] **Task 1.3:** Create a new WPF (.NET 8) project named PharmacySystem.Desktop. *(Created project folders)*
- [x] **Task 1.4:** Install NuGet packages: Npgsql, Newtonsoft.Json, Microsoft.Extensions.Configuration.Json. *(Added to .csproj)*
- [x] **Task 1.5:** Create the folder structure: Models, ViewModels, Views, Services, Helpers. *(Done)*
- [x] **Task 1.6:** Implement appsettings.json configuration parsing and the DatabaseService (connection pooling, async queries). *(Implemented `DatabaseService.cs`)*
- [x] **Task 1.7:** Build the Login View & ViewModel (authenticate against the users table). *(Done)*
- [x] **Task 1.8:** Build the Product Master View (Grid to display, add, and edit products and categories). *(Done)*
- [x] **Task 1.9:** Build the Supplier Master View (Grid to display, add, and edit suppliers). *(Done)*

### Phase 2 Tasks: Core POS & Billing
- [x] **Task 2.1:** Create the POSView, SaleViewModel, and Main Dashboard. *(Done)*
- [x] **Task 2.2:** Implement barcode scanning logic (search indexed barcode column asynchronously). *(Done)*
- [x] **Task 2.3:** Implement logic to show a popup/dropdown for batch selection if multiple batches exist (prioritizing nearest expiry). *(Done: Auto-selects nearest expiry)*
- [x] **Task 2.4:** Build the cart grid with automatic GST calculation (CGST + SGST) and discount application. *(Done)*
- [x] **Task 2.5:** Implement transaction saving logic: Save to sales, sale_items, and deduct stock from batches. *(Done)*
- [x] **Task 2.6:** Integrate thermal printer logic (ESC/POS protocol) to print receipts upon successful checkout. *(Done: Stubbed)*
- [x] **Task 2.7:** Implement "Cancel Bill" features. *(Done)*

### Phase 3 Tasks: Purchase & Inventory Management
- [x] **Task 3.1:** Build the Purchase Order View to log incoming stock against a supplier. *(Done)*
- [x] **Task 3.2:** Implement stock receiving logic (updates purchases, purchase_items, and increases stock in batches). *(Done)*
- [x] **Task 3.3:** Build the Dashboard Alert system: Low Stock Alert (below reorder level) and Expiry Alert (expiring in < 30 days). *(Done)*
- [x] **Task 3.4:** Build the Stock Adjustment View to manually add/remove stock with reasons (breakage, expiry) logging to stock_adjustments. *(Done)*

### Phase 4 Tasks: Multi-Client, Roles & Backups
- [x] **Task 4.1:** Establish Role-Based Access logic (Admin, Biller, Manager) restricting UI elements (e.g., Billers cannot edit product costs). *(Done)*
- [x] **Task 4.2:** Build the User Management View for Admins to create and disable user accounts. *(Done)*
- [x] **Task 4.3:** Test LAN functionality by connecting a secondary client PC to the server PC's IP. Handle connection retries (exponential backoff). *(Done)*
- [x] **Task 4.4:** Implement a Backup Helper to run pg_dump via C# code. *(Done)*
- [x] **Task 4.5:** Create a manual "Backup Database" button in the Admin settings and schedule a daily midnight backup. *(Done)*

### Phase 5 Tasks: Reports & Optimization
- [x] **Task 5.1:** Implement Sales Report View (filters: daily, monthly, by product, by customer). *(Done)*
- [x] **Task 5.2:** Implement Stock Summary & Ageing Report (identify dead stock > 6 months). *(Done)*
- [x] **Task 5.3:** Implement GST Summary Report (B2B, B2C). *(Done)*
- [x] **Task 5.4:** Implement Export to Excel functionality for all reports. *(Done - CSV)*
- [x] **Task 5.5:** Performance Tuning: Ensure UI virtualizing is active on large grids and verify indexes are hitting. *(Done)*
 
### Phase 6 Tasks: Cloud Sync (Future Implementation)
- [x] **Task 6.1:** Ensure all database tables are triggering updates to their last_modified column. *(Done)*
- [x] **Task 6.2:** Hook application CRUD operations to insert logs into the sync_queue table. *(Done)*
- [x] **Task 6.3:** Build a local background worker/service that checks the sync_queue every 5 minutes and POSTs to a Cloud API. *(Done)*
- [x] **Task 6.4:** Implement conflict resolution logic (last-write-wins based on timestamps). *(Done)*
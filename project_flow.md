# Pharmacy Management System – Project Flow Overview

## 1. System Architecture
*   **Architecture:** Desktop-First, Client-Server model. One PC acts as the **Server** (hosting the PostgreSQL database), and other PCs act as **Clients** (running the WPF application). If used on a single PC, it operates in Standalone mode.
*   **Data Communication:** The client WPF applications communicate with the PostgreSQL server over the Local Area Network (LAN) using direct TCP/IP (`Npgsql` library). All database calls are asynchronous to keep the UI responsive.
*   **Design Pattern:** Model-View-ViewModel (MVVM) pattern.
    *   *Views:* XAML files for UI.
    *   *ViewModels:* C# classes handling logic and data binding.
    *   *Models:* C# classes reflecting database tables.

## 2. Performance & Concurrency Measures
*   **Pagination and Virtualization:** Used to handle large datasets (e.g., millions of products) without UI freezing.
*   **Connection Pooling:** Enabled to efficiently manage multiple LAN clients querying the database simultaneously.
*   **Transaction Management:** Concurrency is handled via database transactions (`ReadCommitted` isolation level) to ensure stock deductions and sales are strictly isolated.

## 3. Offline First & Future Sync
*   **Offline Operation:** The system runs completely offline. Every table tracks `last_modified` using PostgreSQL triggers.
*   **Cloud Sync Architecture:** Later, a background service will read a `sync_queue` table and periodically push data to an AWS/Cloud endpoint when the internet is available. It uses a "last-write-wins" conflict resolution logic based on timestamps.

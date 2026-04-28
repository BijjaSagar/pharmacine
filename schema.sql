-- Create database (run separately if needed)
-- CREATE DATABASE PharmacyDB;
-- \c PharmacyDB;

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users table
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL CHECK (role IN ('Admin', 'Biller', 'Manager')),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Categories
CREATE TABLE categories (
    category_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    parent_id INT REFERENCES categories(category_id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Suppliers
CREATE TABLE suppliers (
    supplier_id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    gst VARCHAR(20),
    contact_person VARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(100),
    address TEXT,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Products
CREATE TABLE products (
    product_id SERIAL PRIMARY KEY,
    barcode VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    generic_name VARCHAR(200),
    category_id INT REFERENCES categories(category_id),
    pack_size VARCHAR(50),
    hsn_code VARCHAR(50) DEFAULT '3004',
    reorder_level INT DEFAULT 0,
    unit_price DECIMAL(10,2) NOT NULL,
    gst_percent DECIMAL(5,2) DEFAULT 5.0,
    is_prescription_required BOOLEAN DEFAULT false,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Batches (stock with expiry)
CREATE TABLE batches (
    batch_id SERIAL PRIMARY KEY,
    product_id INT NOT NULL REFERENCES products(product_id),
    batch_number VARCHAR(50) NOT NULL,
    mfg_date DATE,
    expiry_date DATE NOT NULL,
    quantity DECIMAL(10,2) NOT NULL CHECK (quantity >= 0),
    cost_price DECIMAL(10,2) NOT NULL,
    mrp DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(product_id, batch_number)
);

-- Customers (patients)
CREATE TABLE customers (
    customer_id SERIAL PRIMARY KEY,
    name VARCHAR(150),
    mobile VARCHAR(15) NOT NULL,
    email VARCHAR(100),
    address TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Sales (invoice header)
CREATE TABLE sales (
    sale_id SERIAL PRIMARY KEY,
    invoice_no VARCHAR(20) UNIQUE NOT NULL,
    customer_id INT REFERENCES customers(customer_id),
    sale_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    subtotal DECIMAL(10,2) NOT NULL,
    discount_amount DECIMAL(10,2) DEFAULT 0,
    taxable_amount DECIMAL(10,2) NOT NULL,
    cgst DECIMAL(10,2) DEFAULT 0,
    sgst DECIMAL(10,2) DEFAULT 0,
    grand_total DECIMAL(10,2) NOT NULL,
    payment_mode VARCHAR(20) DEFAULT 'Cash',
    user_id INT REFERENCES users(user_id),
    is_cancelled BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Sale items
CREATE TABLE sale_items (
    sale_item_id SERIAL PRIMARY KEY,
    sale_id INT NOT NULL REFERENCES sales(sale_id) ON DELETE CASCADE,
    batch_id INT NOT NULL REFERENCES batches(batch_id),
    quantity DECIMAL(10,2) NOT NULL CHECK (quantity > 0),
    selling_price DECIMAL(10,2) NOT NULL,
    discount_percent DECIMAL(5,2) DEFAULT 0,
    subtotal DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Purchases
CREATE TABLE purchases (
    purchase_id SERIAL PRIMARY KEY,
    supplier_id INT NOT NULL REFERENCES suppliers(supplier_id),
    invoice_no VARCHAR(50) UNIQUE NOT NULL,
    purchase_date DATE NOT NULL,
    total_cost DECIMAL(10,2) NOT NULL,
    user_id INT REFERENCES users(user_id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Purchase items
CREATE TABLE purchase_items (
    purchase_item_id SERIAL PRIMARY KEY,
    purchase_id INT NOT NULL REFERENCES purchases(purchase_id) ON DELETE CASCADE,
    product_id INT NOT NULL REFERENCES products(product_id),
    batch_id INT REFERENCES batches(batch_id),  -- can be null if batch not yet created
    quantity DECIMAL(10,2) NOT NULL,
    cost_price DECIMAL(10,2) NOT NULL,
    mrp DECIMAL(10,2) NOT NULL,
    expiry_date DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Stock adjustment log
CREATE TABLE stock_adjustments (
    adjustment_id SERIAL PRIMARY KEY,
    batch_id INT NOT NULL REFERENCES batches(batch_id),
    old_quantity DECIMAL(10,2) NOT NULL,
    new_quantity DECIMAL(10,2) NOT NULL,
    reason VARCHAR(255),
    user_id INT REFERENCES users(user_id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Sync queue (for future cloud)
CREATE TABLE sync_queue (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(50) NOT NULL,
    record_id INT NOT NULL,
    operation VARCHAR(10) NOT NULL CHECK (operation IN ('INSERT','UPDATE','DELETE')),
    payload_json TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending','synced','failed')),
    retry_count INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_attempt TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_products_barcode ON products(barcode);
CREATE INDEX idx_products_name ON products(name);
CREATE INDEX idx_batches_product_id ON batches(product_id);
CREATE INDEX idx_batches_expiry_date ON batches(expiry_date);
CREATE INDEX idx_sales_invoice_no ON sales(invoice_no);
CREATE INDEX idx_sales_sale_date ON sales(sale_date);
CREATE INDEX idx_sale_items_sale_id ON sale_items(sale_id);
CREATE INDEX idx_purchases_supplier_id ON purchases(supplier_id);
CREATE INDEX idx_stock_adjustments_batch_id ON stock_adjustments(batch_id);
CREATE INDEX idx_sync_queue_status ON sync_queue(status);

-- Auto-update last_modified trigger function
CREATE OR REPLACE FUNCTION update_last_modified_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.last_modified = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply trigger to all tables with last_modified
CREATE TRIGGER update_users_last_modified BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_categories_last_modified BEFORE UPDATE ON categories FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_suppliers_last_modified BEFORE UPDATE ON suppliers FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_products_last_modified BEFORE UPDATE ON products FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_batches_last_modified BEFORE UPDATE ON batches FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_customers_last_modified BEFORE UPDATE ON customers FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_sales_last_modified BEFORE UPDATE ON sales FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();
CREATE TRIGGER update_purchases_last_modified BEFORE UPDATE ON purchases FOR EACH ROW EXECUTE FUNCTION update_last_modified_column();

-- Sample test data (optional)
INSERT INTO categories (name) VALUES ('Antibiotics'), ('Pain Relief'), ('Vitamins');
INSERT INTO suppliers (name, phone) VALUES ('ABC Pharma', '9876543210'), ('XYZ Medicos', '9876543211');
INSERT INTO products (barcode, name, unit_price) VALUES ('8901234567890', 'Paracetamol 500mg', 25.00);
INSERT INTO batches (product_id, batch_number, expiry_date, quantity, cost_price, mrp) 
VALUES (1, 'B001', '2025-12-31', 100, 18.00, 25.00);
INSERT INTO customers (name, mobile) VALUES ('Walk-in Customer', '0000000000');
INSERT INTO users (username, password_hash, role) VALUES ('admin', 'admin123', 'Admin');  -- change hash later

-- Performance Indexes (Phase 5)
CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);
CREATE INDEX IF NOT EXISTS idx_batches_product_id ON batches(product_id);
CREATE INDEX IF NOT EXISTS idx_sales_sale_date ON sales(sale_date);
CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items(sale_id);

-- Phase 6: Cloud Sync
CREATE TABLE IF NOT EXISTS sync_queue (
    sync_id SERIAL PRIMARY KEY,
    table_name VARCHAR(50) NOT NULL,
    record_id INT NOT NULL,
    operation VARCHAR(10) NOT NULL, -- INSERT, UPDATE, DELETE
    sync_status VARCHAR(20) DEFAULT 'PENDING', -- PENDING, COMPLETED, FAILED
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE OR REPLACE FUNCTION log_sync_event()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'DELETE') THEN
        INSERT INTO sync_queue (table_name, record_id, operation) 
        VALUES (TG_TABLE_NAME, OLD.batch_id, 'DELETE'); -- Simplified, assumes batch_id, needs dynamic logic or specific triggers per table in prod
        RETURN OLD;
    ELSIF (TG_OP = 'UPDATE') THEN
        -- Just an example for the batches table, for a robust sync it needs to be mapped to the primary key of each table
        RETURN NEW;
    ELSIF (TG_OP = 'INSERT') THEN
        RETURN NEW;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;



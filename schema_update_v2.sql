-- Features Update SQL
ALTER TABLE products ADD COLUMN IF NOT EXISTS shelf_location VARCHAR(50) DEFAULT 'Store';

-- Users update for RBAC
ALTER TABLE users ADD COLUMN IF NOT EXISTS role VARCHAR(20) DEFAULT 'Cashier';
ALTER TABLE users ADD COLUMN IF NOT EXISTS override_pin VARCHAR(10) DEFAULT '0000';

-- Sales update for WhatsApp/Chronic tracking
ALTER TABLE sales ADD COLUMN IF NOT EXISTS is_chronic BOOLEAN DEFAULT false;
ALTER TABLE sales ADD COLUMN IF NOT EXISTS refill_due_date DATE NULL;
ALTER TABLE sales ADD COLUMN IF NOT EXISTS whatsapp_sent BOOLEAN DEFAULT false;

-- Barcode generations
CREATE TABLE IF NOT EXISTS barcode_print_queue (
    queue_id SERIAL PRIMARY KEY,
    product_id INT REFERENCES products(product_id),
    batch_id INT REFERENCES batches(batch_id),
    print_quantity INT DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

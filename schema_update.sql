-- Updates for Pro features
ALTER TABLE products ADD COLUMN IF NOT EXISTS is_schedule_h1 BOOLEAN DEFAULT false;

ALTER TABLE customers ADD COLUMN IF NOT EXISTS credit_limit DECIMAL(10,2) DEFAULT 0;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS outstanding_balance DECIMAL(10,2) DEFAULT 0;

ALTER TABLE sales ADD COLUMN IF NOT EXISTS customer_name VARCHAR(150);
ALTER TABLE sales ADD COLUMN IF NOT EXISTS customer_phone VARCHAR(15);
ALTER TABLE sales ADD COLUMN IF NOT EXISTS doctor_name VARCHAR(150);
ALTER TABLE sales ADD COLUMN IF NOT EXISTS patient_address TEXT;

-- We also need to add missing columns that the UI was trying to write
ALTER TABLE sales ADD COLUMN IF NOT EXISTS total_gst DECIMAL(10,2) DEFAULT 0;
ALTER TABLE sales ADD COLUMN IF NOT EXISTS total_amount DECIMAL(10,2) DEFAULT 0;

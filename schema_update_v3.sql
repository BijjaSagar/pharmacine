-- Khata System / Credit Book Updates
ALTER TABLE customers ADD COLUMN IF NOT EXISTS credit_limit DECIMAL(10,2) DEFAULT 5000.00;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS outstanding_balance DECIMAL(10,2) DEFAULT 0.00;

ALTER TABLE sales ADD COLUMN IF NOT EXISTS payment_mode VARCHAR(20) DEFAULT 'Cash';

-- Customer ledger for tracking payments and credit
CREATE TABLE IF NOT EXISTS customer_ledger (
    ledger_id SERIAL PRIMARY KEY,
    customer_id INT REFERENCES customers(customer_id),
    sale_id INT REFERENCES sales(sale_id) NULL,
    transaction_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    transaction_type VARCHAR(20) NOT NULL, -- 'CREDIT' or 'PAYMENT'
    amount DECIMAL(10,2) NOT NULL,
    notes TEXT
);

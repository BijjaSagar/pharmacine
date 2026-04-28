<?php
// ClinicOS Pharmacy License Key Generator (PHP)

$secret = "PharmacyKeyMaker2026!";
$salt = "Ivan Medvedev";
$iterations = 1000;

// C# Rfc2898DeriveBytes generates Key and IV consecutively from the same sequence.
// We request 48 bytes total (32 for Key + 16 for IV)
$keyIvData = hash_pbkdf2("sha1", $secret, $salt, $iterations, 48, true);

$key = substr($keyIvData, 0, 32);
$iv = substr($keyIvData, 32, 16);

$generatedKey = "";

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $hwid = trim($_POST["hwid"] ?? "");
    $days = (int)($_POST["days"] ?? 365);
    
    if (!empty($hwid) && $days > 0) {
        $date = new DateTime();
        $date->modify("+$days days");
        $expiry = $date->format('c'); // ISO 8601 string, C# DateTime.TryParse accepts this perfectly
        
        $payload = $hwid . "|" . $expiry;
        
        // C# Encoding.Unicode is UTF-16LE
        $payloadUtf16 = mb_convert_encoding($payload, 'UTF-16LE', 'UTF-8');
        
        // Encrypt with AES-256-CBC and PKCS7 padding (default in openssl_encrypt)
        $cipherText = openssl_encrypt($payloadUtf16, 'AES-256-CBC', $key, OPENSSL_RAW_DATA, $iv);
        
        $generatedKey = base64_encode($cipherText);
    }
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ClinicOS - License Generator</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f0f4f8; margin: 0; padding: 40px; }
        .container { max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }
        h1 { color: #1e293b; margin-top: 0; }
        .form-group { margin-bottom: 20px; }
        label { display: block; font-weight: 600; margin-bottom: 8px; color: #475569; }
        input[type="text"], input[type="number"] { width: 100%; padding: 10px; border: 1px solid #cbd5e1; border-radius: 4px; box-sizing: border-box; font-size: 16px; }
        button { background-color: #0ea5e9; color: white; border: none; padding: 12px 20px; font-size: 16px; font-weight: bold; border-radius: 4px; cursor: pointer; width: 100%; }
        button:hover { background-color: #0284c7; }
        .result { margin-top: 30px; padding: 20px; background-color: #ecfdf5; border: 1px solid #10b981; border-radius: 4px; }
        .result h3 { color: #065f46; margin-top: 0; }
        .key-box { font-family: monospace; word-break: break-all; background: #fff; padding: 10px; border: 1px dashed #10b981; font-size: 16px; }
    </style>
</head>
<body>

<div class="container">
    <h1>🔑 ClinicOS Key Generator</h1>
    <p style="color:#64748b; margin-bottom:30px;">Generate license keys for client installations.</p>
    
    <form method="POST">
        <div class="form-group">
            <label for="hwid">Client Hardware ID:</label>
            <input type="text" id="hwid" name="hwid" required placeholder="e.g. 00-14-22-01-23-45">
        </div>
        
        <div class="form-group">
            <label for="days">Duration (Days):</label>
            <input type="number" id="days" name="days" required value="365" min="1">
        </div>
        
        <button type="submit">Generate License Key</button>
    </form>
    
    <?php if ($generatedKey): ?>
    <div class="result">
        <h3>✅ Key Generated Successfully</h3>
        <p>Send this key to the client:</p>
        <div class="key-box"><?php echo htmlspecialchars($generatedKey); ?></div>
    </div>
    <?php endif; ?>
</div>

</body>
</html>

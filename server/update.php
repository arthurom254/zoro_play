<?php
/**
 * Release server endpoint.
 *   GET  update.php?action=latest   -> {"version","file","sha256","size","downloadUrl"}
 *   POST update.php?action=upload   -> header X-Secret-Key: <uuid>, fields: version, file (.exe)
 * Files land in ./releases/ ; latest.json + version.txt are updated atomically.
 * Needs upload_max_filesize / post_max_size larger than the installer (~100 MB).
 */
declare(strict_types=1);

const SECRET_KEY = 'REPLACE-WITH-A-UUID';   // e.g. 3f9c1a52-8b7e-4d1c-9a63-2e5d7c0b4f18
const PRODUCT    = 'ZoroPlay';

$dir    = __DIR__ . '/releases';
$latest = $dir . '/latest.json';
header('Content-Type: application/json');
header('Cache-Control: no-store');

function out(int $code, array $data): never {
    http_response_code($code);
    echo json_encode($data, JSON_UNESCAPED_SLASHES);
    exit;
}

$action = $_GET['action'] ?? 'latest';
$method = $_SERVER['REQUEST_METHOD'];

if ($action === 'latest' && $method === 'GET') {
    if (!is_file($latest)) out(404, ['error' => 'no release yet']);
    readfile($latest);
    exit;
}

if ($action === 'upload' && $method === 'POST') {
    $key = $_SERVER['HTTP_X_SECRET_KEY'] ?? '';
    if (!hash_equals(SECRET_KEY, $key)) { usleep(500000); out(403, ['error' => 'forbidden']); }

    $version = trim($_POST['version'] ?? '');
    if (!preg_match('/^\d+\.\d+\.\d+$/', $version)) out(400, ['error' => 'bad version']);

    if (!isset($_FILES['file']) || $_FILES['file']['error'] !== UPLOAD_ERR_OK)
        out(400, ['error' => 'upload failed', 'code' => $_FILES['file']['error'] ?? -1]);
    if (strtolower(pathinfo($_FILES['file']['name'], PATHINFO_EXTENSION)) !== 'exe')
        out(400, ['error' => 'only .exe accepted']);

    if (!is_dir($dir) && !mkdir($dir, 0755, true)) out(500, ['error' => 'cannot create releases dir']);

    if (is_file($latest)) {
        $cur = json_decode((string)file_get_contents($latest), true)['version'] ?? '0.0.0';
        if (version_compare($version, $cur, '<')) out(409, ['error' => "older than current $cur"]);
    }

    $name = PRODUCT . "-Setup-$version.exe";
    $tmp  = "$dir/$name.part";
    if (!move_uploaded_file($_FILES['file']['tmp_name'], $tmp)) out(500, ['error' => 'move failed']);
    rename($tmp, "$dir/$name");

    $scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
    $base   = $scheme . '://' . $_SERVER['HTTP_HOST'] . rtrim(dirname($_SERVER['SCRIPT_NAME']), '/');
    $meta = [
        'version'     => $version,
        'file'        => $name,
        'sha256'      => strtoupper(hash_file('sha256', "$dir/$name")),
        'size'        => filesize("$dir/$name"),
        'downloadUrl' => "$base/releases/$name",
        'released'    => gmdate('c'),
    ];
    file_put_contents("$latest.tmp", json_encode($meta, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT), LOCK_EX);
    rename("$latest.tmp", $latest);
    file_put_contents("$dir/version.txt", $version . "\n", LOCK_EX);

    out(200, ['ok' => true] + $meta);
}

out(400, ['error' => 'bad request']);

<?php

// Parse a resume with the HireLayer Resume Parser (POST /api/v4/resumes/parse).
//
//   composer require guzzlehttp/guzzle:^7.8
//   php parse_resume.php [resume.pdf]
//
// 1 credit per resume; 0 when the same file is already stored.

declare(strict_types=1);

require __DIR__ . '/vendor/autoload.php';

use GuzzleHttp\Client;
use GuzzleHttp\Psr7\Utils;
use Psr\Http\Message\ResponseInterface;

const MAX_ATTEMPTS = 4;

$apiKey = getenv('HIRELAYER_API_KEY') ?: throw new RuntimeException('HIRELAYER_API_KEY is not set');
$path = $argv[1] ?? 'resume.pdf';

$http = new Client([
    'base_uri' => 'https://hirelayer.co/api/v4/',
    'timeout' => 150,         // the API answers or gives up within 145 s
    'connect_timeout' => 10,
    'http_errors' => false,   // read 4xx/5xx bodies yourself
    'headers' => [
        'X-API-Key' => $apiKey,
        'Accept' => 'application/json',
    ],
]);

// The form is built again on each attempt, so a retry sends the whole file.
[$status, $data] = post($http, 'resumes/parse', fn () => [
    'multipart' => [
        [
            'name' => 'file',
            'contents' => Utils::tryFopen($path, 'rb'),
            'filename' => basename($path),
        ],
    ],
]);

if ($status >= 400) {
    // The v4 error format: {"error": {"code", "message", "param", "doc_url"}}. Branch on the code.
    $error = $data['error'] ?? ['code' => 'unexpected_response', 'message' => "HTTP $status", 'doc_url' => null];
    fwrite(STDERR, match ($error['code']) {
        'insufficient_credits' => 'Out of credits: see https://hirelayer.co/pricing',
        'resume_unreadable' => "$path could not be read as a resume (not billed)",
        default => sprintf('HTTP %d %s: %s (%s)', $status, $error['code'], $error['message'], $error['doc_url'] ?? ''),
    } . PHP_EOL);
    exit(1);
}

$resume = $data['resume']; // the parsed resume

echo 'application_id: ', $data['application_id'] ?? '(not stored)', PHP_EOL;
echo 'job title: ', $resume['info_candidate']['job_title'] ?? '(none found)', PHP_EOL;
echo 'positions: ', count($resume['work_experiences'] ?? []), PHP_EOL;
echo 'skills: ', implode(', ', array_column($resume['skills'] ?? [], 'skill_title')), PHP_EOL;
echo 'photo found: ', empty($resume['info_resume']['face_url']) ? 'no' : 'yes', PHP_EOL;
foreach ($data['warnings'] as $warning) {
    echo 'warning: ', $warning, PHP_EOL;
}
echo 'credits used: ', $data['credits_used'], PHP_EOL;

/**
 * POST with the options built by $options (again on each attempt).
 * Retries 429 after Retry-After, and 502/503 with backoff. Returns [status, decoded JSON body].
 */
function post(Client $http, string $path, callable $options): array
{
    for ($attempt = 1; ; $attempt++) {
        $response = $http->post($path, $options());
        $status = $response->getStatusCode();
        if (in_array($status, [429, 502, 503], true) && $attempt < MAX_ATTEMPTS) {
            sleep(retryDelay($response, $attempt));
            continue;
        }
        $data = json_decode((string) $response->getBody(), true);
        return [$status, is_array($data) ? $data : []];
    }
}

function retryDelay(ResponseInterface $response, int $attempt): int
{
    $header = $response->getHeaderLine('Retry-After');
    $retryAfter = preg_match('/^\d+$/', $header) === 1 ? (int) $header : null;
    if ($response->getStatusCode() === 429 && $retryAfter !== null) {
        return $retryAfter;
    }
    return max($retryAfter ?? 0, 2 ** $attempt); // 2, 4, 8 s
}

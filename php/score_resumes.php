<?php

// Score resumes against a job with the HireLayer Resume Scorer (POST /api/v4/resumes/score).
//
//   composer require guzzlehttp/guzzle:^7.8
//   php score_resumes.php [job.txt] [resume1.pdf resume2.pdf ...]
//
// 1 credit per resume evaluated, +1 per resume file parsed in the call; 0 for a pair already scored.

declare(strict_types=1);

require __DIR__ . '/vendor/autoload.php';

use GuzzleHttp\Client;
use GuzzleHttp\Psr7\Utils;
use Psr\Http\Message\ResponseInterface;

const MAX_ATTEMPTS = 4;

$apiKey = getenv('HIRELAYER_API_KEY') ?: throw new RuntimeException('HIRELAYER_API_KEY is not set');
$jobPath = $argv[1] ?? 'job.txt';
$resumePaths = array_slice($argv, 2) ?: ['resume.pdf'];

$jobText = file_get_contents($jobPath);
if ($jobText === false) {
    throw new RuntimeException("Cannot read $jobPath");
}

$http = new Client([
    'base_uri' => 'https://hirelayer.co/api/v4/',
    'timeout' => 150,         // seconds, the client timeout the API docs recommend
    'connect_timeout' => 10,
    'http_errors' => false,   // read 4xx/5xx bodies yourself
    'headers' => [
        'X-API-Key' => $apiKey,
        'Accept' => 'application/json',
    ],
]);

// The call as JSON in the "request" part; each resume file in the part it names.
$request = [
    'job' => ['text' => $jobText],
    'resumes' => array_map(
        fn (int $i, string $path) => ['file' => 'cv' . ($i + 1), 'reference' => basename($path)],
        array_keys($resumePaths),
        $resumePaths,
    ),
];

// The form is built again on each attempt, so a retry sends every file.
[$status, $data] = post($http, 'resumes/score', function () use ($request, $resumePaths) {
    $multipart = [[
        'name' => 'request',
        'contents' => json_encode($request, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE),
        'headers' => ['Content-Type' => 'application/json'],
    ]];
    foreach ($resumePaths as $i => $path) {
        $multipart[] = [
            'name' => 'cv' . ($i + 1),
            'contents' => Utils::tryFopen($path, 'rb'),
            'filename' => basename($path),
        ];
    }
    return ['multipart' => $multipart];
});

if ($status >= 400) {
    // The v4 error format: {"error": {"code", "message", "param", "doc_url"}}. Branch on the code.
    $error = $data['error'] ?? ['code' => 'unexpected_response', 'message' => "HTTP $status", 'doc_url' => null];
    fwrite(STDERR, match ($error['code']) {
        'insufficient_credits' => 'Out of credits: see https://hirelayer.co/pricing',
        'job_unreadable' => "$jobPath could not be read as a job description (not billed)",
        default => sprintf('HTTP %d %s: %s (%s)', $status, $error['code'], $error['message'], $error['doc_url'] ?? ''),
    } . PHP_EOL);
    exit(1);
}

if ($status === 202) {
    // A large call: the scores are computed in the background.
    echo 'Accepted, results at: ', $data['results_url'], PHP_EOL;
    exit(0);
}

foreach ($data['scores'] as $score) { // best first
    if ($score['evaluation_status'] !== 'completed') {
        printf("%d. %s: %s (%s)\n", $score['rank'], $score['reference'], $score['evaluation_status'], $score['error']['code'] ?? '');
        continue;
    }
    printf("%d. %s: %.2f %s\n", $score['rank'], $score['reference'], $score['score'], $score['verdict'] ?? '(no verdict)');
    printf("   %s\n", $score['summary'] ?? '');
}
echo 'job_id: ', $data['job']['job_id'] ?? '(not stored)', PHP_EOL;
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

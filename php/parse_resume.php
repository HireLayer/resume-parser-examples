<?php

declare(strict_types=1);

require __DIR__ . '/vendor/autoload.php';

use GuzzleHttp\Client;
use GuzzleHttp\Psr7\Utils;

$apiKey = getenv('HIRELAYER_API_KEY') ?: throw new RuntimeException('HIRELAYER_API_KEY is not set');

$http = new Client([
    'base_uri' => 'https://hirelayer.co',
    'timeout' => 150,         // the API answers or gives up within 145 s
    'connect_timeout' => 10,
    'http_errors' => false,   // read 4xx/5xx bodies yourself
]);

$response = $http->post('/api/v3/parser', [
    'headers' => [
        'X-API-Key' => $apiKey,
        'Accept' => 'application/json',
    ],
    'multipart' => [
        [
            'name' => 'file',
            'contents' => Utils::tryFopen('resume.pdf', 'rb'),
            'filename' => 'resume.pdf',
        ],
        ['name' => 'application_id', 'contents' => 'candidate-123'],
        ['name' => 'do_not_store_data', 'contents' => 'true'],
    ],
]);

$data = json_decode((string) $response->getBody(), true, 512, JSON_THROW_ON_ERROR);

if ($response->getStatusCode() !== 200) {
    throw new RuntimeException(sprintf('HTTP %d: %s', $response->getStatusCode(), $data['error'] ?? 'unknown error'));
}

echo $data['info_candidate']['full_name'] ?? '(no name found)', PHP_EOL;
echo count($data['work_experiences']), ' positions', PHP_EOL;

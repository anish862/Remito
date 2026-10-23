import http from 'k6/http';
import { check } from 'k6';

const apiBaseUrl = (__ENV.API_BASE_URL || 'http://localhost:5191/api/v1').replace(/\/$/, '');

function hasHeader(headers, name) {
  return Object.keys(headers).some((header) => header.toLowerCase() === name.toLowerCase());
}

export const options = {
  stages: [
    { duration: '1m', target: 100 },
    { duration: '2m', target: 100 },
    { duration: '2m', target: 1000 },
    { duration: '3m', target: 1000 },
    { duration: '2m', target: 5000 },
    { duration: '5m', target: 5000 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<400'],
    http_req_failed: ['rate<0.005'],
    checks: ['rate>0.995'],
  },
};

export default function () {
  const response = http.get(`${apiBaseUrl}/rates/latest?base=OMR&symbols=INR`, {
    headers: { 'Cache-Control': 'no-cache' },
  });
  check(response, {
    'latest rates returned successfully': (res) => res.status === 200,
    'latest rates advertise cache validators': (res) =>
      hasHeader(res.headers, 'etag') && hasHeader(res.headers, 'cache-control'),
  });
}

import http from 'k6/http';
import { check } from 'k6';

const apiBaseUrl = (__ENV.API_BASE_URL || 'http://localhost:5191/api/v1').replace(/\/$/, '');

function hasHeader(headers, name) {
  return Object.keys(headers).some((header) => header.toLowerCase() === name.toLowerCase());
}

export const options = {
  stages: [
    { duration: '30s', target: 20 },
    { duration: '60s', target: 20 },
    { duration: '15s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<200'],
    checks: ['rate>0.99'],
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

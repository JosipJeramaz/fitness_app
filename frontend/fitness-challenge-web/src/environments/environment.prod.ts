// Production build (Docker/nginx): the reverse proxy forwards /api to the backend container.
export const environment = {
  production: true,
  apiBaseUrl: '/api',
};

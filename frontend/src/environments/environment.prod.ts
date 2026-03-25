export const environment = {
  production: true,
  apiUrl: (window as any).__env?.apiUrl || 'http://localhost:5000/api/v1',
  appName: 'Investment Mate v2'
};
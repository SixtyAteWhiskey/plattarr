declare module '*.module.css';

interface Window {
  Radarr: {
    apiKey: string;
    calendarFeedKey: string;
    apiRoot: string;
    instanceName: string;
    theme: string;
    urlBase: string;
    version: string;
    isProduction: boolean;
  };
}

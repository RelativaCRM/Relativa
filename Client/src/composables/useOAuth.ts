/// <reference types="google.accounts" />
import { ref } from 'vue';
import {
  PublicClientApplication,
  type Configuration,
} from '@azure/msal-browser';
import { currentLocale } from '@/i18n';
import { GOOGLE_CLIENT_ID, MS_CLIENT_ID, MS_AUTHORITY } from '@/config/oauth';

declare global {
  interface Window {
    google?: typeof google;
  }
}

const GIS_SRC = 'https://accounts.google.com/gsi/client';

const googleClientId = GOOGLE_CLIENT_ID;
const msClientId = MS_CLIENT_ID;
const msAuthority = MS_AUTHORITY;

export const googleEnabled = Boolean(googleClientId);
export const microsoftEnabled = Boolean(msClientId);
export const anyOAuthEnabled = googleEnabled || microsoftEnabled;

let gisPromise: Promise<void> | null = null;

function loadGis(locale: string): Promise<void> {
  if (gisPromise) return gisPromise;
  gisPromise = new Promise((resolve, reject) => {
    if (window.google?.accounts?.oauth2) {
      resolve();
      return;
    }
    const script = document.createElement('script');
    script.src = `${GIS_SRC}?hl=${encodeURIComponent(locale)}`;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('gis_load_failed'));
    document.head.appendChild(script);
  });
  return gisPromise;
}

let msalInstance: PublicClientApplication | null = null;
let msalReady: Promise<PublicClientApplication> | null = null;

function getMsal(): Promise<PublicClientApplication> {
  if (msalReady) return msalReady;
  const config: Configuration = {
    auth: {
      clientId: msClientId!,
      authority: msAuthority,
      redirectUri: `${window.location.origin}/redirect.html`,
    },
    cache: { cacheLocation: 'sessionStorage' },
  };
  msalInstance = new PublicClientApplication(config);
  msalReady = msalInstance
    .initialize()
    .then(() => msalInstance!.handleRedirectPromise())
    .then(() => msalInstance!);
  return msalReady;
}

let googleTokenClient: google.accounts.oauth2.TokenClient | null = null;
let googleTokenHandler: ((accessToken: string) => void) | null = null;

async function getGoogleTokenClient(): Promise<google.accounts.oauth2.TokenClient> {
  if (googleTokenClient) return googleTokenClient;
  await loadGis(currentLocale());
  googleTokenClient = window.google!.accounts.oauth2.initTokenClient({
    client_id: googleClientId!,
    scope: 'openid email profile',
    callback: (response) => {
      if (response.access_token) {
        googleTokenHandler?.(response.access_token);
      }
    },
  });
  return googleTokenClient;
}

export function useOAuth() {
  const busy = ref(false);

  function warmGoogle() {
    if (!googleEnabled) return;
    void getGoogleTokenClient();
  }

  function signInWithGoogle(onToken: (accessToken: string) => void) {
    if (!googleEnabled) return;
    googleTokenHandler = onToken;
    if (googleTokenClient) {
      googleTokenClient.requestAccessToken({ prompt: 'select_account' });
      return;
    }
    void getGoogleTokenClient().then((client) =>
      client.requestAccessToken({ prompt: 'select_account' }),
    );
  }

  async function signInWithMicrosoft(): Promise<string> {
    if (!microsoftEnabled) {
      throw new Error('microsoft_not_configured');
    }
    busy.value = true;
    try {
      const msal = await getMsal();
      const result = await msal.loginPopup({
        scopes: ['openid', 'email', 'profile'],
        prompt: 'select_account',
        overrideInteractionInProgress: true,
      });
      if (!result.idToken) {
        throw new Error('microsoft_no_id_token');
      }
      return result.idToken;
    } finally {
      busy.value = false;
    }
  }

  return {
    busy,
    googleEnabled,
    microsoftEnabled,
    anyOAuthEnabled,
    warmGoogle,
    signInWithGoogle,
    signInWithMicrosoft,
  };
}

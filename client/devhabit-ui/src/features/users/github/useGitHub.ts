import { useAuth0 } from '@auth0/auth0-react';
import { fetchWithAuth } from '../../../utils/fetchUtils';
import { API_BASE_URL } from '../../../api/config';
import type { Link } from '../../../types/api';

export interface GitHubUserProfile {
  login: string;
  name?: string;
  avatar_url: string;
  bio?: string;
  public_repos: number;
  followers: number;
  following: number;
  links: Link[];
}

export function useGitHub() {
  const { getAccessTokenSilently } = useAuth0();

  const submitPAT = async (personalAccessToken: string, expiresInDays: number) => {
    const accessToken = await getAccessTokenSilently();
    await fetchWithAuth(`${API_BASE_URL}/github/personal-access-token`, accessToken, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ accessToken: personalAccessToken, expiresInDays }),
    });
  };

  const getProfile = async (): Promise<GitHubUserProfile | null> => {
    try {
      const accessToken = await getAccessTokenSilently();
      const response = await fetchWithAuth<GitHubUserProfile>(
        `${API_BASE_URL}/github/profile`,
        accessToken,
        {
          headers: {
            Accept: 'application/vnd.dev-habit.hateoas+json',
          },
        }
      );
      return response;
    } catch {
      return null;
    }
  };

  const revokePAT = async (link: Link): Promise<void> => {
    if (link.rel !== 'revoke-token' || link.method !== 'DELETE') {
      throw new Error('Invalid operation: Link does not support token revocation');
    }

    const accessToken = await getAccessTokenSilently();
    await fetchWithAuth(link.href, accessToken, {
      method: link.method,
      headers: {
        Accept: 'application/vnd.dev-habit.hateoas+json',
      },
    });
  };

  return {
    submitPAT,
    getProfile,
    revokePAT,
  };
}

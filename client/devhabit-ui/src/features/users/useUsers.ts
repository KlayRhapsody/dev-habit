import { useAuth0 } from '@auth0/auth0-react';
import { fetchWithAuth } from '../../utils/fetchUtils';
import { API_BASE_URL } from '../../api/config';
import type { HateoasResponse, Link } from '../../types/api';

export interface UserProfile extends HateoasResponse {
  id: string;
  email: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export function useUsers() {
  const { getAccessTokenSilently } = useAuth0();

  const getProfile = async (): Promise<UserProfile | null> => {
    try {
      const accessToken = await getAccessTokenSilently();
      return await fetchWithAuth<UserProfile>(`${API_BASE_URL}/users/me`, accessToken, {
        headers: {
          Accept: 'application/vnd.dev-habit.hateoas+json',
        },
      });
    } catch (error) {
      console.error('Failed to fetch user profile:', error);
      return null;
    }
  };

  const updateProfile = async (name: string, link: Link): Promise<boolean> => {
    if (link.rel !== 'update-profile' || link.method !== 'PUT') {
      throw new Error('Invalid operation: Link does not support profile update');
    }

    try {
      const accessToken = await getAccessTokenSilently();
      await fetchWithAuth<UserProfile>(link.href, accessToken, {
        method: link.method,
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ name }),
      });
      return true;
    } catch (error) {
      console.error('Failed to update user profile:', error);
      return false;
    }
  };

  return {
    getProfile,
    updateProfile,
  };
}

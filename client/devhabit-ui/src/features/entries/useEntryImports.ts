import { useState } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { API_BASE_URL } from '../../api/config';
import type { EntryImportJob, EntryImportJobsResponse } from './types';
import { fetchWithAuth } from '../../utils/fetchUtils';
import type { Link } from '../../types/api';

interface ListImportsOptions {
  pageSize?: number;
  url?: string;
}

export function useEntryImports() {
  const { getAccessTokenSilently } = useAuth0();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const listImports = async ({
    pageSize = 10,
    url,
  }: ListImportsOptions = {}): Promise<EntryImportJobsResponse | null> => {
    setIsLoading(true);
    setError(null);

    try {
      const accessToken = await getAccessTokenSilently();
      const result = await fetchWithAuth<EntryImportJobsResponse>(
        url ?? `${API_BASE_URL}/entries/imports?pageSize=${pageSize}`,
        accessToken,
        {
          headers: {
            Accept: 'application/vnd.dev-habit.hateoas+json',
          },
        }
      );
      return result;
    } catch (err: any) {
      setError(err.message || 'Failed to load import jobs');
      return null;
    } finally {
      setIsLoading(false);
    }
  };

  const uploadFile = async (file: File): Promise<EntryImportJob | null> => {
    setIsLoading(true);
    setError(null);

    try {
      const formData = new FormData();
      formData.append('file', file);

      const accessToken = await getAccessTokenSilently();
      const result = await fetchWithAuth<EntryImportJob>(
        `${API_BASE_URL}/entries/imports`,
        accessToken,
        {
          method: 'POST',
          headers: {
            Accept: 'application/vnd.dev-habit.hateoas+json',
          },
          body: formData,
        }
      );

      return result;
    } catch (err: any) {
      setError(err.message || 'Failed to upload file');
      return null;
    } finally {
      setIsLoading(false);
    }
  };

  const getImport = async (link: Link): Promise<EntryImportJob | null> => {
    if (link.rel !== 'self' || link.method !== 'GET') {
      throw new Error('Invalid operation: Link does not support fetching import job');
    }

    setIsLoading(true);
    setError(null);

    try {
      const accessToken = await getAccessTokenSilently();
      const result = await fetchWithAuth<EntryImportJob>(link.href, accessToken, {
        headers: {
          Accept: 'application/vnd.dev-habit.hateoas+json',
        },
      });
      return result;
    } catch (err: any) {
      setError(err.message || 'Failed to load import job');
      return null;
    } finally {
      setIsLoading(false);
    }
  };

  return {
    listImports,
    uploadFile,
    getImport,
    isLoading,
    error,
  };
}

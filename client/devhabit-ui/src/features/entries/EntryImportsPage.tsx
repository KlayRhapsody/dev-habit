import React, { useEffect, useState, useRef } from 'react';
import { useEntryImports } from './useEntryImports';
import type { EntryImportJob } from './types';
import type { Link as HypermediaLink } from '../../types/api';
import { EntryImportStatus } from './types';
import { useNavigate } from 'react-router-dom';

export const EntryImportsPage: React.FC = () => {
  const { listImports, uploadFile, isLoading, error } = useEntryImports();
  const [imports, setImports] = useState<EntryImportJob[]>([]);
  const [nextPageLink, setNextPageLink] = useState<HypermediaLink | null>(null);
  const [prevPageLink, setPrevPageLink] = useState<HypermediaLink | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [errorMessage, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    loadImports();
  }, []);

  const loadImports = async () => {
    const result = await listImports({ pageSize: 6 });
    if (result) {
      setImports(result.items);
      setNextPageLink(result.links.find(l => l.rel === 'next-page') || null);
      setPrevPageLink(result.links.find(l => l.rel === 'previous-page') || null);
    }
  };

  const handlePageChange = async (link: HypermediaLink) => {
    const result = await listImports({ url: link.href });
    if (result) {
      setImports(result.items);
      setNextPageLink(result.links.find(l => l.rel === 'next-page') || null);
      setPrevPageLink(result.links.find(l => l.rel === 'previous-page') || null);
    }
  };

  const handleFileSelect = async (file: File) => {
    if (!file.name.toLowerCase().endsWith('.csv')) {
      setError('Please select a CSV file');
      return;
    }

    const result = await uploadFile(file);
    if (result) {
      await loadImports();
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);

    const file = e.dataTransfer.files[0];
    if (file) {
      await handleFileSelect(file);
    }
  };

  const getStatusColor = (status: EntryImportStatus) => {
    switch (status) {
      case EntryImportStatus.Completed:
        return 'bg-green-100 text-green-800';
      case EntryImportStatus.Failed:
        return 'bg-red-100 text-red-800';
      case EntryImportStatus.Processing:
        return 'bg-yellow-100 text-yellow-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  if (error || errorMessage) {
    return (
      <div className="max-w-4xl mx-auto p-6">
        <div className="p-3 bg-red-100 text-red-700 rounded-md">{error || errorMessage}</div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto p-6">
      <div className="mb-8">
        <div className="flex justify-between items-center mb-4">
          <h1 className="text-2xl font-semibold">Import Entries</h1>
          <button
            onClick={() => navigate('/entries')}
            className="text-blue-600 hover:text-blue-800"
          >
            ← Back to Entries
          </button>
        </div>

        <div
          className={`border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors
            ${isDragging ? 'border-blue-500 bg-blue-50' : 'border-gray-300 hover:border-gray-400'}`}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => fileInputRef.current?.click()}
        >
          <input
            type="file"
            ref={fileInputRef}
            className="hidden"
            accept=".csv"
            onChange={e => e.target.files?.[0] && handleFileSelect(e.target.files[0])}
          />
          <div className="text-gray-600">
            <p className="mb-2">Drag and drop your CSV file here</p>
            <p className="text-sm">or click to select a file</p>
            <p className="mt-4 text-xs text-gray-500">Supported format: CSV</p>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        <h2 className="text-xl font-semibold mb-4">Recent Imports</h2>

        {isLoading && imports.length === 0 ? (
          <div className="space-y-4">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="animate-pulse">
                <div className="h-24 bg-gray-100 rounded-lg"></div>
              </div>
            ))}
          </div>
        ) : imports.length === 0 ? (
          <div className="text-center py-12">
            <p className="text-gray-500">No imports found. Upload a CSV file to get started.</p>
          </div>
        ) : (
          <div className="space-y-4">
            {imports.map(importJob => (
              <div
                key={importJob.id}
                className="bg-white rounded-lg shadow hover:shadow-md transition-shadow p-4"
              >
                <div className="flex justify-between items-start">
                  <div>
                    <h3 className="font-medium">{importJob.fileName}</h3>
                    <div className="flex items-center gap-4 mt-2 text-sm text-gray-500">
                      <span>Created: {new Date(importJob.createdAtUtc).toLocaleString()}</span>
                      {importJob.processedAtUtc && (
                        <span>
                          Processed: {new Date(importJob.processedAtUtc).toLocaleString()}
                        </span>
                      )}
                    </div>
                    {importJob.errorMessage && (
                      <p className="mt-2 text-sm text-red-600">{importJob.errorMessage}</p>
                    )}
                  </div>
                  <span
                    className={`px-3 py-1 rounded-full text-sm ${getStatusColor(importJob.status)}`}
                  >
                    {EntryImportStatus[importJob.status]}
                  </span>
                </div>
              </div>
            ))}

            <div className="flex justify-center gap-4 mt-6">
              {prevPageLink && (
                <button
                  onClick={() => handlePageChange(prevPageLink)}
                  className="px-4 py-2 text-blue-600 hover:text-blue-800 cursor-pointer"
                  disabled={isLoading}
                >
                  ← Previous
                </button>
              )}
              {nextPageLink && (
                <button
                  onClick={() => handlePageChange(nextPageLink)}
                  className="px-4 py-2 text-blue-600 hover:text-blue-800 cursor-pointer"
                  disabled={isLoading}
                >
                  Next →
                </button>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

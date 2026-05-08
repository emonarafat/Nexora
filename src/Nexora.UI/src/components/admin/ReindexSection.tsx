'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { triggerReindex, getReindexStatus } from '../../services/adminApi';
import type { ReindexJob } from '../../types/admin';

export function ReindexSection() {
  const [currentJobId, setCurrentJobId] = useState<string | null>(null);
  const [isFullReindex, setIsFullReindex] = useState(false);

  // Fetch current job status
  const { data: jobStatus } = useQuery<ReindexJob | null>({
    queryKey: ['reindexStatus', currentJobId],
    queryFn: () => (currentJobId ? getReindexStatus(currentJobId) : Promise.resolve(null)),
    enabled: !!currentJobId,
    refetchInterval: currentJobId ? 2000 : false, // Poll every 2 seconds while job is active
  });

  // Trigger reindex mutation
  const triggerMutation = useMutation({
    mutationFn: (fullReindex: boolean) => triggerReindex(fullReindex),
    onSuccess: (data) => {
      setCurrentJobId(data.jobId);
    },
  });

  // Auto-refetch when job completes
  useEffect(() => {
    if (jobStatus?.status === 'completed' || jobStatus?.status === 'failed') {
      // Job is done, stop polling
    }
  }, [jobStatus?.status]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'pending':
        return 'bg-yellow-50 text-yellow-800 border-yellow-200';
      case 'running':
        return 'bg-blue-50 text-blue-800 border-blue-200';
      case 'completed':
        return 'bg-green-50 text-green-800 border-green-200';
      case 'failed':
        return 'bg-red-50 text-red-800 border-red-200';
      default:
        return 'bg-slate-50 text-slate-800 border-slate-200';
    }
  };

  const getProgressPercentage = (job: ReindexJob | null) => {
    if (!job || job.totalDocuments === 0) return 0;
    return Math.round((job.documentsProcessed / job.totalDocuments) * 100);
  };

  return (
    <div className="space-y-6 p-6">
      {/* Reindex Controls */}
      <div className="space-y-4 rounded-lg bg-slate-50 p-4">
        <h3 className="font-semibold text-slate-900">Trigger Reindex</h3>

        <div className="flex items-center gap-3">
          <input
            type="checkbox"
            id="fullReindex"
            checked={isFullReindex}
            onChange={(e) => setIsFullReindex(e.target.checked)}
            className="rounded border-slate-300"
          />
          <label htmlFor="fullReindex" className="text-sm text-slate-700">
            Full reindex (clears existing index and rebuilds from scratch)
          </label>
        </div>

        <button
          onClick={() => triggerMutation.mutate(isFullReindex)}
          disabled={
            triggerMutation.isPending ||
            (jobStatus?.status === 'running' || jobStatus?.status === 'pending')
          }
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:bg-slate-300"
        >
          {triggerMutation.isPending ? 'Starting...' : 'Trigger Reindex'}
        </button>

        <p className="text-xs text-slate-600">
          {isFullReindex
            ? 'Full reindex will replace the entire search index. This may take several minutes.'
            : 'Incremental reindex will sync changes since the last run.'}
        </p>
      </div>

      {/* Job Status */}
      {jobStatus && (
        <div className={`rounded-lg border p-4 ${getStatusColor(jobStatus.status)}`}>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h4 className="font-semibold">Job ID: {jobStatus.jobId}</h4>
              <span className="rounded-full bg-white/50 px-3 py-1 text-xs font-medium">
                {jobStatus.status.toUpperCase()}
              </span>
            </div>

            {/* Progress Bar */}
            {(jobStatus.status === 'running' || jobStatus.status === 'pending') && (
              <div className="space-y-1">
                <div className="flex justify-between text-xs">
                  <span>Progress</span>
                  <span>
                    {jobStatus.documentsProcessed} / {jobStatus.totalDocuments}
                  </span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-white/30">
                  <div
                    className="h-full bg-white/70 transition-all duration-500"
                    style={{ width: `${getProgressPercentage(jobStatus)}%` }}
                  />
                </div>
              </div>
            )}

            {/* Completion Info */}
            {(jobStatus.status === 'completed' || jobStatus.status === 'failed') && (
              <div className="space-y-1 text-xs">
                <p>
                  <strong>Started:</strong> {new Date(jobStatus.startedAt).toLocaleString()}
                </p>
                {jobStatus.completedAt && (
                  <p>
                    <strong>Completed:</strong> {new Date(jobStatus.completedAt).toLocaleString()}
                  </p>
                )}
                <p>
                  <strong>Documents Processed:</strong> {jobStatus.documentsProcessed} /{' '}
                  {jobStatus.totalDocuments}
                </p>
                {jobStatus.errorMessage && (
                  <p className="text-red-700">
                    <strong>Error:</strong> {jobStatus.errorMessage}
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {!jobStatus && (
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-4 text-center">
          <p className="text-sm text-slate-600">No active reindex job. Trigger one to get started.</p>
        </div>
      )}

      {/* Information Box */}
      <div className="space-y-2 rounded-lg bg-blue-50 p-4 border border-blue-200">
        <h4 className="text-sm font-semibold text-blue-900">ℹ️ Reindex Information</h4>
        <ul className="space-y-1 text-xs text-blue-800">
          <li>• Reindexing syncs product data from MSSQL to Typesense</li>
          <li>• Full reindex clears and rebuilds the entire index (~2-5 minutes)</li>
          <li>• Incremental reindex only processes changed documents (~30-60 seconds)</li>
          <li>• Search continues to work during reindexing</li>
        </ul>
      </div>
    </div>
  );
}

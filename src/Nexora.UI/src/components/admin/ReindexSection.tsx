'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { triggerReindex, getReindexStatus } from '../../services/adminApi';
import type { ReindexJob } from '../../types/admin';

type AdminActionStatus = 'success' | 'error';

interface ReindexSectionProps {
  onAction: (action: string, target: string, status: AdminActionStatus) => void;
}

function isTerminalStatus(status?: ReindexJob['status']): boolean {
  return status === 'completed' || status === 'failed';
}

export function ReindexSection({ onAction }: ReindexSectionProps) {
  const [currentJobId, setCurrentJobId] = useState<string | null>(null);
  const [hasRequestedStatus, setHasRequestedStatus] = useState(false);
  const [latestTriggeredJob, setLatestTriggeredJob] = useState<ReindexJob | null>(null);

  // Fetch current job status
  const { data: jobStatus, error: statusError } = useQuery<ReindexJob>({
    queryKey: ['reindexStatus', currentJobId],
    queryFn: () => getReindexStatus(currentJobId ?? undefined),
    enabled: hasRequestedStatus,
    refetchInterval: (query) => {
      const status = (query.state.data as ReindexJob | undefined)?.status;
      if (!status || isTerminalStatus(status)) return false;
      return 2000;
    },
  });

  // Trigger reindex mutation
  const triggerMutation = useMutation({
    mutationFn: triggerReindex,
    onSuccess: (data) => {
      setCurrentJobId(data.jobId ?? null);
      setLatestTriggeredJob(data);
      setHasRequestedStatus(true);
      onAction('Trigger reindex', data.jobId ?? 'current index', 'success');
    },
    onError: () => {
      onAction('Trigger reindex', 'current index', 'error');
    },
  });

  const statusToRender = jobStatus ?? latestTriggeredJob;

  const getStatusColor = (status: ReindexJob['status']) => {
    switch (status) {
      case 'accepted':
        return 'bg-slate-50 text-slate-800 border-slate-200';
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

  const getProgressPercentage = (job: ReindexJob) => {
    if (!job.totalDocuments || job.totalDocuments === 0) return 0;
    return Math.round(((job.documentsProcessed ?? 0) / job.totalDocuments) * 100);
  };

  return (
    <div className="space-y-6 p-6">
      {/* Reindex Controls */}
      <div className="space-y-4 rounded-lg bg-slate-50 p-4">
        <h3 className="font-semibold text-slate-900">Trigger Reindex</h3>

        <button
          onClick={() => triggerMutation.mutate()}
          disabled={triggerMutation.isPending || statusToRender?.status === 'running' || statusToRender?.status === 'pending'}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:bg-slate-300"
        >
          {triggerMutation.isPending ? 'Starting...' : 'Trigger Reindex'}
        </button>

        <p className="text-xs text-slate-600">
          Reindexing syncs the catalog into search storage and may take several minutes.
        </p>
      </div>

      {/* Job Status */}
      {statusToRender && (
        <div className={`rounded-lg border p-4 ${getStatusColor(statusToRender.status)}`}>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h4 className="font-semibold">Job ID: {statusToRender.jobId ?? 'Not provided'}</h4>
              <span className="rounded-full bg-white/50 px-3 py-1 text-xs font-medium">
                {statusToRender.status.toUpperCase()}
              </span>
            </div>
            {statusToRender.message ? <p className="text-xs">{statusToRender.message}</p> : null}

            {/* Progress Bar */}
            {(statusToRender.status === 'running' || statusToRender.status === 'pending') && (
              <div className="space-y-1">
                <div className="flex justify-between text-xs">
                  <span>Progress</span>
                  <span>
                    {statusToRender.documentsProcessed ?? 0} / {statusToRender.totalDocuments ?? 0}
                  </span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-white/30">
                  <div
                    className="h-full bg-white/70 transition-all duration-500"
                    style={{ width: `${getProgressPercentage(statusToRender)}%` }}
                  />
                </div>
              </div>
            )}

            {/* Completion Info */}
            {isTerminalStatus(statusToRender.status) && (
              <div className="space-y-1 text-xs">
                {statusToRender.startedAt ? (
                  <p>
                    <strong>Started:</strong> {new Date(statusToRender.startedAt).toLocaleString()}
                  </p>
                ) : null}
                {statusToRender.completedAt && (
                  <p>
                    <strong>Completed:</strong> {new Date(statusToRender.completedAt).toLocaleString()}
                  </p>
                )}
                <p>
                  <strong>Documents Processed:</strong> {statusToRender.documentsProcessed ?? 0} /{' '}
                  {statusToRender.totalDocuments ?? 0}
                </p>
                {statusToRender.errorMessage && (
                  <p className="text-red-700">
                    <strong>Error:</strong> {statusToRender.errorMessage}
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {statusError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800">
          Unable to load reindex status right now.
        </div>
      ) : null}

      {!statusToRender && (
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

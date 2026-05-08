'use client';

import { useEffect, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { fetchRankingConfig, updateRankingConfig } from '../../services/adminApi';
import type { RankingConfigRequest } from '../../types/admin';

const WEIGHT_CONSTRAINTS = {
  textScoreWeight: { min: 0, max: 1, step: 0.05 },
  availabilityWeight: { min: 0, max: 1, step: 0.05 },
  ratingWeight: { min: 0, max: 1, step: 0.05 },
  popularityWeight: { min: 0, max: 1, step: 0.05 },
};

type AdminActionStatus = 'success' | 'error';

interface RankingConfigSectionProps {
  onAction: (action: string, target: string, status: AdminActionStatus) => void;
}

export function RankingConfigSection({ onAction }: RankingConfigSectionProps) {
  const queryClient = useQueryClient();
  const [formData, setFormData] = useState<RankingConfigRequest>({
    textScoreWeight: 0.4,
    availabilityWeight: 0.2,
    ratingWeight: 0.2,
    popularityWeight: 0.2,
  });
  const [hasChanges, setHasChanges] = useState(false);

  // Fetch ranking config
  const { data: config, isLoading, error } = useQuery({
    queryKey: ['rankingConfig'],
    queryFn: fetchRankingConfig,
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: (data: RankingConfigRequest) => updateRankingConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rankingConfig'] });
      setHasChanges(false);
      onAction('Update ranking config', 'ranking weights', 'success');
    },
    onError: () => {
      onAction('Update ranking config', 'ranking weights', 'error');
    },
  });

  // Update form when config loads
  useEffect(() => {
    if (config) {
      setFormData({
        textScoreWeight: config.textScoreWeight,
        availabilityWeight: config.availabilityWeight,
        ratingWeight: config.ratingWeight,
        popularityWeight: config.popularityWeight,
      });
    }
  }, [config]);

  const handleChange = (key: keyof RankingConfigRequest, value: number) => {
    setFormData((prev) => ({ ...prev, [key]: value }));
    setHasChanges(true);
  };

  const handleSave = () => {
    updateMutation.mutate(formData);
  };

  const getTotalWeight = () => {
    return (
      formData.textScoreWeight +
      formData.availabilityWeight +
      formData.ratingWeight +
      formData.popularityWeight
    ).toFixed(2);
  };

  const totalWeight = parseFloat(getTotalWeight());
  const isValidTotal = Math.abs(totalWeight - 1.0) < 0.01;

  if (isLoading) {
    return (
      <div className="p-6 text-center">
        <p className="text-slate-600">Loading ranking configuration...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <div className="rounded-lg bg-red-50 p-4">
          <p className="text-sm text-red-800">
            Error loading ranking config. Make sure the backend API is running at http://localhost:5000
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Configuration Info */}
      {config && (
        <div className="rounded-lg bg-slate-50 p-4">
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div>
               <p className="text-slate-600">Last Updated</p>
               <p className="font-semibold text-slate-900">
                 {config.lastUpdatedAt ? new Date(config.lastUpdatedAt).toLocaleDateString() : 'N/A'}
               </p>
             </div>
            <div>
              <p className="text-slate-600">Updated By</p>
              <p className="font-semibold text-slate-900">{config.lastUpdatedBy ?? 'System'}</p>
            </div>
          </div>
        </div>
      )}

      {/* Weight Controls */}
      <div className="space-y-4">
        <h3 className="font-semibold text-slate-900">Ranking Weights</h3>

        {/* Text Score Weight */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <label className="text-sm font-medium text-slate-900">Text Score Weight</label>
            <span className="text-sm font-semibold text-blue-600">{formData.textScoreWeight.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min={WEIGHT_CONSTRAINTS.textScoreWeight.min}
            max={WEIGHT_CONSTRAINTS.textScoreWeight.max}
            step={WEIGHT_CONSTRAINTS.textScoreWeight.step}
            value={formData.textScoreWeight}
            onChange={(e) => handleChange('textScoreWeight', parseFloat(e.target.value))}
            className="w-full"
          />
          <p className="text-xs text-slate-600">Full-text search relevance score</p>
        </div>

        {/* Availability Weight */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <label className="text-sm font-medium text-slate-900">Availability Weight</label>
            <span className="text-sm font-semibold text-blue-600">{formData.availabilityWeight.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min={WEIGHT_CONSTRAINTS.availabilityWeight.min}
            max={WEIGHT_CONSTRAINTS.availabilityWeight.max}
            step={WEIGHT_CONSTRAINTS.availabilityWeight.step}
            value={formData.availabilityWeight}
            onChange={(e) => handleChange('availabilityWeight', parseFloat(e.target.value))}
            className="w-full"
          />
          <p className="text-xs text-slate-600">In-stock products receive a proportional boost</p>
        </div>

        {/* Rating Weight */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <label className="text-sm font-medium text-slate-900">Rating Weight</label>
            <span className="text-sm font-semibold text-blue-600">
              {formData.ratingWeight.toFixed(2)}
            </span>
          </div>
          <input
            type="range"
            min={WEIGHT_CONSTRAINTS.ratingWeight.min}
            max={WEIGHT_CONSTRAINTS.ratingWeight.max}
            step={WEIGHT_CONSTRAINTS.ratingWeight.step}
            value={formData.ratingWeight}
            onChange={(e) => handleChange('ratingWeight', parseFloat(e.target.value))}
            className="w-full"
          />
          <p className="text-xs text-slate-600">Higher-rated products receive stronger ranking influence</p>
        </div>

        {/* Popularity Weight */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <label className="text-sm font-medium text-slate-900">Popularity Weight</label>
            <span className="text-sm font-semibold text-blue-600">
              {formData.popularityWeight.toFixed(2)}
            </span>
          </div>
          <input
            type="range"
            min={WEIGHT_CONSTRAINTS.popularityWeight.min}
            max={WEIGHT_CONSTRAINTS.popularityWeight.max}
            step={WEIGHT_CONSTRAINTS.popularityWeight.step}
            value={formData.popularityWeight}
            onChange={(e) => handleChange('popularityWeight', parseFloat(e.target.value))}
            className="w-full"
          />
          <p className="text-xs text-slate-600">Overall product popularity</p>
        </div>

        {/* Total Weight Indicator */}
        <div
          className={`rounded-lg border p-3 ${
            isValidTotal
              ? 'bg-green-50 text-green-800 border-green-200'
              : 'bg-red-50 text-red-800 border-red-200'
          }`}
        >
          <p className="text-sm font-semibold">
            Total Weight: {getTotalWeight()} {isValidTotal ? '✓' : '✗'}
          </p>
          <p className="text-xs">Weights must sum to 1.0 for normalization</p>
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <button
          onClick={handleSave}
          disabled={!hasChanges || !isValidTotal || updateMutation.isPending}
          className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:bg-slate-300"
        >
          {updateMutation.isPending ? 'Saving...' : 'Save Configuration'}
        </button>
        <button
          onClick={() => {
            if (config) {
              setFormData({
                textScoreWeight: config.textScoreWeight,
                availabilityWeight: config.availabilityWeight,
                ratingWeight: config.ratingWeight,
                popularityWeight: config.popularityWeight,
              });
              setHasChanges(false);
            }
          }}
          disabled={!hasChanges}
          className="rounded-lg bg-slate-400 px-4 py-2 text-sm font-medium text-white hover:bg-slate-500 disabled:bg-slate-200"
        >
          Reset
        </button>
      </div>

      {/* Information Box */}
      <div className="space-y-2 rounded-lg bg-blue-50 p-4 border border-blue-200">
        <h4 className="text-sm font-semibold text-blue-900">ℹ️ Ranking Formula</h4>
        <p className="text-xs text-blue-800">
          Final Score = (Text Score × {formData.textScoreWeight.toFixed(2)}) + (Availability × {formData.availabilityWeight.toFixed(2)}) +
          (Rating × {formData.ratingWeight.toFixed(2)}) + (Popularity × {formData.popularityWeight.toFixed(2)})
        </p>
        <p className="text-xs text-blue-800">Adjust weights to optimize ranking for your business objectives.</p>
      </div>
    </div>
  );
}

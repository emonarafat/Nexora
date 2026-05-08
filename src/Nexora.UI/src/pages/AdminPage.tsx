'use client';

import { useState } from 'react';
import { SynonymsSection } from '../components/admin/SynonymsSection';
import { ReindexSection } from '../components/admin/ReindexSection';
import { RankingConfigSection } from '../components/admin/RankingConfigSection';

type TabType = 'synonyms' | 'reindex' | 'ranking';
type AdminActionStatus = 'success' | 'error';

interface AdminAction {
  id: string;
  action: string;
  target: string;
  status: AdminActionStatus;
  timestamp: string;
}

export function AdminPage() {
  const [activeTab, setActiveTab] = useState<TabType>('synonyms');
  const [actions, setActions] = useState<AdminAction[]>([]);

  const handleAction = (action: string, target: string, status: AdminActionStatus) => {
    setActions((previous) => [
      {
        id: crypto.randomUUID(),
        action,
        target,
        status,
        timestamp: new Date().toISOString(),
      },
      ...previous,
    ].slice(0, 10));
  };

  const tabs: { id: TabType; label: string; icon: string }[] = [
    { id: 'synonyms', label: 'Synonyms', icon: '📚' },
    { id: 'reindex', label: 'Reindex', icon: '🔄' },
    { id: 'ranking', label: 'Ranking Config', icon: '⚙️' },
  ];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">Admin Dashboard</h1>
        <p className="mt-2 text-sm text-slate-600">
          Manage search synonyms, trigger reindexing, and configure ranking weights
        </p>
      </div>

      {/* Tab Navigation */}
      <div className="flex space-x-2 border-b border-slate-200 bg-white">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-4 py-3 text-sm font-medium transition-colors ${
              activeTab === tab.id
                ? 'border-b-2 border-blue-500 text-blue-600'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <span className="mr-2">{tab.icon}</span>
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
        {activeTab === 'synonyms' && <SynonymsSection onAction={handleAction} />}
        {activeTab === 'reindex' && <ReindexSection onAction={handleAction} />}
        {activeTab === 'ranking' && <RankingConfigSection onAction={handleAction} />}
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Recent Admin Actions</h2>
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="text-slate-500">
              <tr>
                <th className="pb-2 font-medium">Time</th>
                <th className="pb-2 font-medium">Action</th>
                <th className="pb-2 font-medium">Target</th>
                <th className="pb-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {actions.length === 0 ? (
                <tr>
                  <td className="py-3 text-slate-500" colSpan={4}>
                    No admin actions recorded yet.
                  </td>
                </tr>
              ) : (
                actions.map((item) => (
                  <tr key={item.id} className="border-t border-slate-100">
                    <td className="py-2 text-slate-700">{new Date(item.timestamp).toLocaleTimeString()}</td>
                    <td className="py-2 text-slate-900">{item.action}</td>
                    <td className="py-2 text-slate-700">{item.target}</td>
                    <td className="py-2">
                      <span
                        className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                          item.status === 'success'
                            ? 'bg-green-100 text-green-800'
                            : 'bg-red-100 text-red-800'
                        }`}
                      >
                        {item.status}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

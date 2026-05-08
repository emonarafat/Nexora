'use client';

import { useState } from 'react';
import { SynonymsSection } from '../components/admin/SynonymsSection';
import { ReindexSection } from '../components/admin/ReindexSection';
import { RankingConfigSection } from '../components/admin/RankingConfigSection';

type TabType = 'synonyms' | 'reindex' | 'ranking';

export function AdminPage() {
  const [activeTab, setActiveTab] = useState<TabType>('synonyms');

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
        {activeTab === 'synonyms' && <SynonymsSection />}
        {activeTab === 'reindex' && <ReindexSection />}
        {activeTab === 'ranking' && <RankingConfigSection />}
      </div>
    </div>
  );
}

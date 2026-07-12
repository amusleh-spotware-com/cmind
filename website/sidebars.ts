import type { SidebarsConfig } from '@docusaurus/plugin-content-docs';

// Curated sidebar — hand-ordered so newcomers get a sensible reading path.
const sidebars: SidebarsConfig = {
  docs: [
    {
      type: 'category',
      label: 'Getting started',
      collapsed: false,
      items: ['intro', 'audience', 'white-label-for-business', 'contributing'],
    },
    {
      type: 'category',
      label: 'Features',
      link: { type: 'doc', id: 'features/README' },
      collapsed: false,
      items: [
        'features/dashboard',
        {
          type: 'category',
          label: 'Copy trading',
          items: [
            'features/copy-trading',
            'features/copy-execution-transparency',
            'features/copy-performance-fees',
            'features/copy-provider-marketplace',
            'features/copy-notifications',
            'features/ai-copy-recommender',
            'features/token-lifecycle',
          ],
        },
        'features/ai',
        'features/build-and-backtest',
        'features/mcp',
        'features/prop-firm',
        'features/two-factor-auth',
        'features/white-label',
        'features/feature-toggles',
        'features/compliance',
        'features/pwa',
      ],
    },
    {
      type: 'category',
      label: 'Architecture',
      collapsed: false,
      items: [
        'architecture',
        {
          type: 'category',
          label: 'Decision records',
          link: { type: 'doc', id: 'adr/README' },
          items: [
            'adr/strict-ddd-pure-core',
            'adr/tph-instance-replaces-entity',
            'adr/external-nodes-http-jwt',
            'adr/cbotbuilder-on-web-host',
            'adr/anthropic-raw-http',
            'adr/copy-profile-db-lease',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Deployment',
      link: { type: 'doc', id: 'deployment/cloud' },
      items: [
        'deployment/local',
        'deployment/cloud-azure',
        'deployment/cloud-aws',
        'deployment/kubernetes',
        'deployment/scaling',
      ],
    },
    {
      type: 'category',
      label: 'Operations',
      items: ['operations/node-discovery', 'operations/logging', 'operations/backup-recovery'],
    },
    {
      type: 'category',
      label: 'Testing',
      items: [
        'testing/dev-credentials',
        'testing/fake-trading-session',
        'testing/failure-paths',
        'testing/live-copy-trading',
        'testing/stress-testing',
        'testing/copy-trading-verification-run',
      ],
    },
    {
      type: 'category',
      label: 'Design',
      items: ['ui-guidelines'],
    },
  ],
};

export default sidebars;

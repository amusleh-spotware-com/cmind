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
        'features/white-label',
        'features/feature-toggles',
        'features/compliance',
        'features/pwa',
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

import { themes as prismThemes } from 'prism-react-renderer';
import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// cMind documentation & marketing site.
// Mint-on-near-black brand, mobile-first, fully SEO'd. Deploys to GitHub Pages.

const organizationName = 'amusleh-spotware-com';
const projectName = 'cmind';
const repoUrl = `https://github.com/${organizationName}/${projectName}`;

const config: Config = {
  title: 'cMind',
  tagline: 'Build, run & backtest cTrader bots across a distributed fleet — with an AI core watching the risk.',
  favicon: 'img/favicon.svg',

  future: {
    v4: true,
    faster: true,
  },

  url: `https://${organizationName}.github.io`,
  baseUrl: `/${projectName}/`,
  organizationName,
  projectName,
  trailingSlash: false,

  // TEMPORARY: 'warn' while docs are being translated locale-by-locale. Relative .md links in a
  // translated doc only resolve once the linked doc is also translated in that locale, so a partial
  // i18n state trips 'throw'. Restore to 'throw' once `npm run i18n:check` is green (all docs, all langs).
  onBrokenLinks: 'warn',

  markdown: {
    mermaid: true,
    // Migrated .md docs contain C# generics like IOptionsMonitor<AppOptions> in prose;
    // 'detect' parses .md as CommonMark (not MDX) so angle brackets never break the build.
    // NOTE: admonition titles MUST use bracket form `:::tip[Title]` — the space form `:::tip Title`
    // (Docusaurus 2 / MDX 1) no longer parses under remark-directive and renders as literal text.
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },
  themes: ['@docusaurus/theme-mermaid'],

  // Docs site speaks the same 23 languages as the product (Core.Constants.SupportedCultures), so a
  // trader reads the app and its manual in one language. Scaffold a locale with
  //   npm run write-translations -- --locale <code>
  // then translate website/i18n/<code>/. Arabic renders right-to-left. Adding/changing a doc means
  // updating every locale in the same PR (see the localization mandate in CLAUDE.md).
  i18n: {
    defaultLocale: 'en',
    locales: [
      'en', 'ar', 'cs', 'de', 'el', 'es', 'fr', 'hu', 'id', 'it', 'ja', 'ko', 'ms',
      'pl', 'pt-BR', 'ru', 'sk', 'sl', 'sr', 'th', 'tr', 'vi', 'zh-Hans',
    ],
    localeConfigs: {
      ar: { direction: 'rtl' },
    },
  },

  headTags: [
    {
      tagName: 'meta',
      attributes: {
        name: 'keywords',
        content:
          'cTrader, cBot, algorithmic trading, copy trading, backtesting, trading bots, prop firm, AI trading, self-hosted, white-label, Blazor, .NET, MCP',
      },
    },
    {
      tagName: 'meta',
      attributes: { name: 'theme-color', content: '#26C281' },
    },
    {
      tagName: 'link',
      attributes: { rel: 'apple-touch-icon', href: 'img/brand/apple-touch-icon.png' },
    },
    {
      tagName: 'script',
      attributes: { type: 'application/ld+json' },
      innerHTML: JSON.stringify({
        '@context': 'https://schema.org',
        '@type': 'SoftwareApplication',
        name: 'cMind',
        applicationCategory: 'FinanceApplication',
        operatingSystem: 'Docker, Kubernetes, Azure, AWS',
        description:
          'Multi-tenant trading operations platform for cTrader: build, backtest, run & copy trading strategies at scale with AI assistance. Self-hostable and white-labelable.',
        offers: { '@type': 'Offer', price: '0', priceCurrency: 'USD' },
        license: 'https://opensource.org/licenses/MIT',
      }),
    },
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          editUrl: `${repoUrl}/tree/main/website/`,
          showLastUpdateTime: true,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
        sitemap: {
          changefreq: 'weekly',
          priority: 0.5,
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/brand/og-image.png',
    metadata: [
      {
        name: 'description',
        content:
          'cMind — a self-hostable, white-label trading operations platform for cTrader. Build, backtest, run and copy trading strategies at scale, with AI built in.',
      },
      { name: 'twitter:card', content: 'summary_large_image' },
      { name: 'twitter:title', content: 'cMind — your entire cTrader trading desk, in one app' },
      { name: 'twitter:description', content: 'Open-source, self-hostable, white-label trading operations platform for cTrader — build, backtest, run & copy strategies at scale, with AI built in.' },
      { name: 'twitter:image:alt', content: 'cMind — your entire cTrader trading desk, in one calm dark app.' },
      { property: 'og:type', content: 'website' },
      { property: 'og:site_name', content: 'cMind' },
      { property: 'og:image:width', content: '1200' },
      { property: 'og:image:height', content: '630' },
      { property: 'og:image:alt', content: 'cMind — your entire cTrader trading desk, in one calm dark app.' },
    ],
    // The brand lives in the dark ("one calm dark app") and the landing page is styled dark-only,
    // so force dark everywhere: a light-OS visitor previously got an unstyled light theme with
    // near-invisible hero text. Lock it, hide the toggle, ignore prefers-color-scheme.
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: true,
      respectPrefersColorScheme: false,
    },
    navbar: {
      title: 'cMind',
      logo: {
        alt: 'cMind logo',
        src: 'img/logo.svg',
      },
      items: [
        { to: '/docs/intro', label: 'Docs', position: 'left' },
        { to: '/docs/features/copy-trading', label: 'Features', position: 'left' },
        { to: '/docs/deployment/local', label: 'Deploy', position: 'left' },
        { to: '/docs/contributing', label: 'Contribute', position: 'left' },
        { to: '/docs/white-label-for-business', label: 'For business', position: 'left' },
        {
          href: repoUrl,
          label: 'GitHub',
          position: 'right',
        },
        { type: 'localeDropdown', position: 'right' },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Get started',
          items: [
            { label: 'Introduction', to: '/docs/intro' },
            { label: 'Quick start', to: '/docs/deployment/local' },
            { label: 'Who it is for', to: '/docs/audience' },
          ],
        },
        {
          title: 'Product',
          items: [
            { label: 'Copy trading', to: '/docs/features/copy-trading' },
            { label: 'AI', to: '/docs/features/ai' },
            { label: 'Build & backtest', to: '/docs/features/build-and-backtest' },
            { label: 'White-label', to: '/docs/features/white-label' },
          ],
        },
        {
          title: 'Deploy',
          items: [
            { label: 'Local (Docker)', to: '/docs/deployment/local' },
            { label: 'Kubernetes', to: '/docs/deployment/kubernetes' },
            { label: 'Azure', to: '/docs/deployment/cloud-azure' },
            { label: 'AWS', to: '/docs/deployment/cloud-aws' },
          ],
        },
        {
          title: 'More',
          items: [
            { label: 'GitHub', href: repoUrl },
            { label: 'Contributing', to: '/docs/contributing' },
            { label: 'Security', href: `${repoUrl}/blob/main/SECURITY.md` },
          ],
        },
      ],
      copyright: `Built with care for traders who move real money. © ${new Date().getFullYear()} cMind · MIT licensed.`,
    },
    prism: {
      theme: prismThemes.oneDark,
      darkTheme: prismThemes.oneDark,
      additionalLanguages: ['csharp', 'bash', 'json', 'yaml', 'docker', 'powershell'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;

import type { ReactNode } from 'react';
import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Translate, { translate } from '@docusaurus/Translate';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import styles from './index.module.css';

// ---- Tiny inline icons (mint, currentColor) so the page stays graphic without extra assets ----
const stroke = {
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.8,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
};

const IconCopy = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <circle cx="6" cy="6" r="2.4" /><circle cx="6" cy="18" r="2.4" /><circle cx="18" cy="12" r="2.4" />
    <path d="M8.2 7.2 15.8 11M8.2 16.8 15.8 13" />
  </svg>
);
const IconAi = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <path d="M12 3v3M12 18v3M3 12h3M18 12h3M6 6l2 2M16 16l2 2M18 6l-2 2M8 16l-2 2" />
    <circle cx="12" cy="12" r="3.2" />
  </svg>
);
const IconCode = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <path d="M8 8l-4 4 4 4M16 8l4 4-4 4M13 5l-2 14" />
  </svg>
);
const IconNodes = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <circle cx="12" cy="5" r="2.2" /><circle cx="5" cy="18" r="2.2" /><circle cx="19" cy="18" r="2.2" />
    <path d="M11 7 6.5 16M13 7l4.5 9M7 18h10" />
  </svg>
);
const IconShield = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <path d="M12 3l7 3v5c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z" /><path d="M9 12l2 2 4-4" />
  </svg>
);
const IconPalette = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <path d="M12 3a9 9 0 1 0 0 18c1.2 0 2-.9 2-2 0-.6-.3-1-.7-1.4-.4-.4-.6-.8-.6-1.3 0-1 .8-1.8 1.8-1.8H16a5 5 0 0 0 5-5c0-3.6-4-6.5-9-6.5z" />
    <circle cx="7.5" cy="12" r="1" /><circle cx="10" cy="8" r="1" /><circle cx="15" cy="8" r="1" />
  </svg>
);
const IconPhone = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <rect x="7" y="3" width="10" height="18" rx="2.4" /><path d="M11 18h2" />
  </svg>
);
const IconPlug = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" {...stroke}>
    <path d="M9 3v4M15 3v4M7 7h10v3a5 5 0 0 1-10 0zM12 15v6" />
  </svg>
);
const IconDocker = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" {...stroke}>
    <rect x="4" y="10" width="3" height="3" /><rect x="8" y="10" width="3" height="3" />
    <rect x="12" y="10" width="3" height="3" /><rect x="8" y="6" width="3" height="3" />
    <path d="M3 13h14a4 4 0 0 0 4-4M17 14c2 2 4 1 4 1" />
  </svg>
);
const IconK8s = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" {...stroke}>
    <path d="M12 3l7 3.5v7L12 21l-7-4v-7z" /><circle cx="12" cy="12" r="2.4" />
    <path d="M12 3v6.6M12 14.4V21M5 6.5l5 4M14 13.5l5 3.5M19 6.5l-5 4M10 13.5l-5 3.5" />
  </svg>
);
const IconCloud = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" {...stroke}>
    <path d="M7 18a4 4 0 0 1-.5-8A6 6 0 0 1 18 9.5 3.5 3.5 0 0 1 17.5 18z" />
  </svg>
);

function Screenshot({ src, alt }: { src: string; alt: string }) {
  return <img src={useBaseUrl(src)} alt={alt} loading="lazy" />;
}

function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.heroGrid} />
      <div className={styles.heroInner}>
        <span className={styles.badge}>
          ● <Translate id="home.hero.badge">Open-source · self-hostable · white-label</Translate>
        </span>
        <Heading as="h1" className={styles.heroTitle}>
          <Translate id="home.hero.title1">Your entire cTrader trading desk,</Translate>{' '}
          <span className="cmind-gradient-text">
            <Translate id="home.hero.title2">in one calm dark app.</Translate>
          </span>
        </Heading>
        <p className={styles.heroSubtitle}>
          <Translate id="home.hero.subtitle">
            cMind builds, backtests, runs, and copies trading strategies across a fleet of machines — with an AI core quietly watching the risk so you don't have to refresh the charts at 3am. Self-host it, brand it as your own, and sleep a little better.
          </Translate>
        </p>
        <div className={styles.heroButtons}>
          <Link className="button button--primary button--lg" to="/docs/intro">
            <Translate id="home.hero.cta1">Get started →</Translate>
          </Link>
          <Link className={`button button--lg ${styles.ghButton}`} to="/docs/deployment/local">
            <Translate id="home.hero.cta2">Run it in 5 minutes</Translate>
          </Link>
        </div>
        <p className={styles.heroNote}>
          <Translate id="home.hero.note">No credit card, no sign-up, no "book a demo". Just</Translate>{' '}
          <code>docker compose up</code>.
        </p>

        <div className={styles.shot}>
          <div className={styles.shotBar}>
            <span className={styles.dot} /><span className={styles.dot} /><span className={styles.dot} />
          </div>
          <Screenshot
            src="/img/screenshots/dashboard-desktop.png"
            alt={translate({ id: 'home.hero.shotAlt', message: 'cMind dashboard' })}
          />
        </div>
      </div>
    </header>
  );
}

function Stats() {
  const stats = [
    { num: '4', label: translate({ id: 'home.stat.deploy', message: 'ways to deploy: Docker, K8s, Azure, AWS' }) },
    { num: '9+', label: translate({ id: 'home.stat.ai', message: 'AI features, not just a chat box' }) },
    { num: '100%', label: translate({ id: 'home.stat.wl', message: 'white-label — ship it as your brand' }) },
    { num: '3', label: translate({ id: 'home.stat.tests', message: 'test tiers guarding every change' }) },
  ];
  return (
    <section className={styles.section}>
      <div className={styles.stats}>
        {stats.map((s) => (
          <div key={s.label} className={styles.stat}>
            <div className={styles.statNum}>{s.num}</div>
            <div className={styles.statLabel}>{s.label}</div>
          </div>
        ))}
      </div>
    </section>
  );
}

function Features() {
  const features = [
    {
      icon: <IconCopy />,
      title: translate({ id: 'home.feat.copy.title', message: 'Copy trading that survives real life' }),
      body: translate({ id: 'home.feat.copy.body', message: 'Mirror a master account onto many accounts across brokers and cTrader IDs. Connections drop, orders bounce, tokens rotate — cMind reconciles without double-firing trades and keeps a full audit trail.' }),
      to: '/docs/features/copy-trading',
      link: translate({ id: 'home.feat.copy.link', message: 'How it works →' }),
    },
    {
      icon: <IconAi />,
      title: translate({ id: 'home.feat.ai.title', message: 'AI that does chores, not small talk' }),
      body: translate({ id: 'home.feat.ai.body', message: 'Plain-English idea → a real, compiling cBot with a self-repair loop. Plus parameter tuning, backtest post-mortems, and a risk guard that can auto-stop a misbehaving bot.' }),
      to: '/docs/features/ai',
      link: translate({ id: 'home.feat.ai.link', message: 'Meet the AI core →' }),
    },
    {
      icon: <IconCode />,
      title: translate({ id: 'home.feat.build.title', message: 'Build & backtest in the browser' }),
      body: translate({ id: 'home.feat.build.body', message: 'A Monaco IDE (yes, the VS Code one), C# and Python templates, and sandboxed dotnet build in throwaway containers. Watch equity curves stream back live.' }),
      to: '/docs/features/build-and-backtest',
      link: translate({ id: 'home.feat.build.link', message: 'Start building →' }),
    },
    {
      icon: <IconNodes />,
      title: translate({ id: 'home.feat.fleet.title', message: 'A fleet that scales & self-heals' }),
      body: translate({ id: 'home.feat.fleet.body', message: 'Runs and backtests spread across auto-discovering nodes. A node dies mid-job? Its lease is reclaimed and work carries on. No pager required.' }),
      to: '/docs/deployment/scaling',
      link: translate({ id: 'home.feat.fleet.link', message: 'Scale out →' }),
    },
    {
      icon: <IconShield />,
      title: translate({ id: 'home.feat.prop.title', message: 'Prop-firm guardrails built in' }),
      body: translate({ id: 'home.feat.prop.body', message: 'Model real challenge rules — daily loss, max drawdown, targets — with live equity tracking, so your bots stay inside the lines.' }),
      to: '/docs/features/prop-firm',
      link: translate({ id: 'home.feat.prop.link', message: 'Pass the challenge →' }),
    },
    {
      icon: <IconPalette />,
      title: translate({ id: 'home.feat.wl.title', message: 'White-label, top to bottom' }),
      body: translate({ id: 'home.feat.wl.body', message: "Swap the name, colors, logo, and favicon per tenant. Resellers ship cMind as their own product and nobody's the wiser." }),
      to: '/docs/features/white-label',
      link: translate({ id: 'home.feat.wl.link', message: 'Make it yours →' }),
    },
    {
      icon: <IconPhone />,
      title: translate({ id: 'home.feat.pwa.title', message: 'In your pocket (installable PWA)' }),
      body: translate({ id: 'home.feat.pwa.body', message: 'Mobile-first, fully responsive, installable on your phone with bottom-nav and an offline shell. Check on your bots from the bus.' }),
      to: '/docs/features/pwa',
      link: translate({ id: 'home.feat.pwa.link', message: 'Install it →' }),
    },
    {
      icon: <IconPlug />,
      title: translate({ id: 'home.feat.mcp.title', message: 'Talks to AI clients (MCP)' }),
      body: translate({ id: 'home.feat.mcp.body', message: 'A built-in MCP server (HTTP + SSE) exposes tools so Claude and friends can drive cMind for you.' }),
      to: '/docs/features/mcp',
      link: translate({ id: 'home.feat.mcp.link', message: 'Wire it up →' }),
    },
    {
      icon: <IconShield />,
      title: translate({ id: 'home.feat.sec.title', message: 'Yours to run, hardened by default' }),
      body: translate({ id: 'home.feat.sec.body', message: 'Argon2id, an encrypted key ring, per-node signed tokens, rate limiting, and OpenTelemetry out of the box. Your keys never leave your box.' }),
      to: '/docs/deployment/local',
      link: translate({ id: 'home.feat.sec.link', message: 'Self-host →' }),
    },
  ];
  return (
    <section className={`${styles.section} ${styles.sectionAlt}`}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}><Translate id="home.feat.eyebrow">Everything in the box</Translate></span>
        <Heading as="h2" className={styles.sectionTitle}>
          <Translate id="home.feat.title">One app. The whole trading operation.</Translate>
        </Heading>
        <p className={styles.sectionLede}>
          <Translate id="home.feat.lede">
            From "I have an idea" to "it's live on twelve accounts" — without gluing together six tools and a spreadsheet.
          </Translate>
        </p>
      </div>
      <div className={styles.features}>
        {features.map((f) => (
          <div key={f.title} className={styles.feature}>
            <div className={styles.featureIcon}>{f.icon}</div>
            <h3 className={styles.featureTitle}>{f.title}</h3>
            <p className={styles.featureText}>{f.body} <Link to={f.to}>{f.link}</Link></p>
          </div>
        ))}
      </div>
    </section>
  );
}

function Showcase() {
  const showcases = [
    {
      img: '/img/screenshots/copy-trading-desktop.png',
      title: translate({ id: 'home.show.copy.title', message: 'Copy trading, minus the panic' }),
      text: translate({ id: 'home.show.copy.text', message: 'Per-destination control over sizing, direction, symbols, order types, SL/TP, expiry and exact market-range slippage. When something goes sideways, cMind resyncs instead of duplicating.' }),
      reverse: false,
    },
    {
      img: '/img/screenshots/ai-build-desktop.png',
      title: translate({ id: 'home.show.ai.title', message: 'Describe it. Watch it compile.' }),
      text: translate({ id: 'home.show.ai.text', message: 'Tell the AI what you want in plain English. It writes the cBot, builds it in a sandbox, reads the errors, and fixes itself — looping until it runs. You review the diff, not the stack trace.' }),
      reverse: true,
    },
    {
      img: '/img/screenshots/nodes-desktop.png',
      title: translate({ id: 'home.show.fleet.title', message: 'A cluster you barely have to think about' }),
      text: translate({ id: 'home.show.fleet.text', message: 'Agents self-register and heartbeat. The scheduler picks the least-loaded node. A dead node hands its work back automatically. It feels like infrastructure because it is.' }),
      reverse: false,
    },
    {
      img: '/img/screenshots/dashboard-mobile.png',
      title: translate({ id: 'home.show.mobile.title', message: 'Runs beautifully on a 360px phone' }),
      text: translate({ id: 'home.show.mobile.text', message: 'Same power, pocket-sized. Bottom-nav, card layouts, offline shell, add-to-home-screen. Every surface is designed mobile-first, then scales up — not the other way around.' }),
      reverse: true,
    },
  ];
  return (
    <section className={styles.section}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}><Translate id="home.show.eyebrow">See it in action</Translate></span>
        <Heading as="h2" className={styles.sectionTitle}>
          <Translate id="home.show.title">Real screenshots. Real dark mode. Real product.</Translate>
        </Heading>
      </div>
      {showcases.map((s) => (
        <div key={s.title} className={`${styles.showcase} ${s.reverse ? styles.showcaseReverse : ''}`}>
          <div className={styles.showcaseImg}>
            <Screenshot src={s.img} alt={s.title} />
          </div>
          <div>
            <h3 className={styles.showcaseTitle}>{s.title}</h3>
            <p className={styles.showcaseText}>{s.text}</p>
          </div>
        </div>
      ))}
    </section>
  );
}

function Deploy() {
  const deploys = [
    { icon: <IconDocker />, name: translate({ id: 'home.deploy.local', message: 'Local / Docker' }), hint: translate({ id: 'home.deploy.local.hint', message: 'compose up & go' }), to: '/docs/deployment/local' },
    { icon: <IconK8s />, name: 'Kubernetes', hint: translate({ id: 'home.deploy.k8s.hint', message: 'Helm chart' }), to: '/docs/deployment/kubernetes' },
    { icon: <IconCloud />, name: 'Azure', hint: translate({ id: 'home.deploy.azure.hint', message: 'step-by-step' }), to: '/docs/deployment/cloud-azure' },
    { icon: <IconCloud />, name: 'AWS', hint: translate({ id: 'home.deploy.aws.hint', message: 'step-by-step' }), to: '/docs/deployment/cloud-aws' },
  ];
  return (
    <section className={`${styles.section} ${styles.sectionAlt}`}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}><Translate id="home.deploy.eyebrow">Deploy anywhere</Translate></span>
        <Heading as="h2" className={styles.sectionTitle}>
          <Translate id="home.deploy.title">Your servers, your rules.</Translate>
        </Heading>
        <p className={styles.sectionLede}>
          <Translate id="home.deploy.lede">
            Beginner-friendly, copy-paste guides for every option. Start on your laptop, graduate to a cluster when you're ready.
          </Translate>
        </p>
      </div>
      <div className={styles.deployGrid}>
        {deploys.map((d) => (
          <Link key={d.name} to={d.to} className={styles.deployCard}>
            <div className={styles.featureIcon}>{d.icon}</div>
            <div>
              <div className={styles.deployName}>{d.name}</div>
              <div className={styles.deployHint}>{d.hint}</div>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}

function Audience() {
  const audience = [
    {
      emoji: '📈',
      title: translate({ id: 'home.aud.traders.title', message: 'Traders' }),
      text: translate({ id: 'home.aud.traders.text', message: 'Self-host your whole desk. Write a cBot, backtest it across nodes, run it live, and copy it — all in one AI-powered console, your data never leaving your box.' }),
      to: '/docs/for-traders',
    },
    {
      emoji: '🏦',
      title: translate({ id: 'home.aud.brokers.title', message: 'Brokers' }),
      text: translate({ id: 'home.aud.brokers.text', message: 'White-label cMind for your clients. Give them AI, copy trading, and prop-firm challenges under your brand, restrict accounts to your book, and open new revenue.' }),
      to: '/docs/for-brokers',
    },
    {
      emoji: '🖥️',
      title: translate({ id: 'home.aud.providers.title', message: 'Cloud & VPS providers' }),
      text: translate({ id: 'home.aud.providers.text', message: 'Offer managed cMind hosting. Land a sticky, compute-hungry workload and monetize the subscription, the metered compute, the white-label, and the AI.' }),
      to: '/docs/for-cloud-providers',
    },
  ];
  return (
    <section className={styles.section}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}><Translate id="home.aud.eyebrow">Who it's for</Translate></span>
        <Heading as="h2" className={styles.sectionTitle}>
          <Translate id="home.aud.title">Built for people with money on the line.</Translate>
        </Heading>
        <p className={styles.sectionLede}>
          <Translate id="home.aud.lede">Trader, broker, or hosting provider —</Translate>{' '}
          <Link to="/docs/audience"><Translate id="home.aud.link">find your path →</Translate></Link>
        </p>
      </div>
      <div className={styles.audience}>
        {audience.map((a) => (
          <Link key={a.title} to={a.to} className={styles.audCard}>
            <div className={styles.audEmoji}>{a.emoji}</div>
            <h3>{a.title}</h3>
            <p>{a.text}</p>
          </Link>
        ))}
      </div>
    </section>
  );
}

function FinalCta() {
  return (
    <section className={styles.cta}>
      <Heading as="h2" className={styles.ctaTitle}>
        <Translate id="home.cta.title">Ready to meet your new trading desk?</Translate>
      </Heading>
      <p className={styles.ctaText}>
        <Translate id="home.cta.text">
          Read the friendly intro, then have it running locally before your coffee gets cold. It's open-source and MIT-licensed — poke around, break things, send a PR.
        </Translate>
      </p>
      <div className={styles.heroButtons}>
        <Link className="button button--primary button--lg" to="/docs/intro">
          <Translate id="home.cta.button1">Read the docs →</Translate>
        </Link>
        <Link
          className={`button button--lg ${styles.ghButton}`}
          href="https://github.com/amusleh-spotware-com/cmind"
        >
          <Translate id="home.cta.button2">Star on GitHub ★</Translate>
        </Link>
      </div>
    </section>
  );
}

export default function Home(): ReactNode {
  return (
    <Layout
      title={translate({ id: 'home.meta.title', message: 'cMind — your entire cTrader trading desk, in one app' })}
      description={translate({ id: 'home.meta.description', message: 'Open-source, self-hostable, white-label trading operations platform for cTrader. Build, backtest, run and copy strategies at scale with AI built in.' })}
    >
      <main className={styles.page}>
        <Hero />
        <Stats />
        <Features />
        <Showcase />
        <Deploy />
        <Audience />
        <FinalCta />
      </main>
    </Layout>
  );
}

import type { ReactNode } from 'react';
import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
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
        <span className={styles.badge}>● Open-source · self-hostable · white-label</span>
        <Heading as="h1" className={styles.heroTitle}>
          Your entire cTrader trading desk,{' '}
          <span className="cmind-gradient-text">in one calm dark app.</span>
        </Heading>
        <p className={styles.heroSubtitle}>
          cMind builds, backtests, runs, and copies trading strategies across a fleet of machines —
          with an AI core quietly watching the risk so you don&apos;t have to refresh the charts at 3am.
          Self-host it, brand it as your own, and sleep a little better.
        </p>
        <div className={styles.heroButtons}>
          <Link className="button button--primary button--lg" to="/docs/intro">
            Get started →
          </Link>
          <Link className={`button button--lg ${styles.ghButton}`} to="/docs/deployment/local">
            Run it in 5 minutes
          </Link>
        </div>
        <p className={styles.heroNote}>
          No credit card, no sign-up, no &quot;book a demo&quot;. Just <code>docker compose up</code>.
        </p>

        <div className={styles.shot}>
          <div className={styles.shotBar}>
            <span className={styles.dot} /><span className={styles.dot} /><span className={styles.dot} />
          </div>
          <Screenshot src="/img/screenshots/dashboard-desktop.png" alt="cMind dashboard" />
        </div>
      </div>
    </header>
  );
}

const stats = [
  { num: '4', label: 'ways to deploy: Docker, K8s, Azure, AWS' },
  { num: '9+', label: 'AI features, not just a chat box' },
  { num: '100%', label: 'white-label — ship it as your brand' },
  { num: '3', label: 'test tiers guarding every change' },
];

function Stats() {
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

const features = [
  {
    icon: <IconCopy />,
    title: 'Copy trading that survives real life',
    body: (
      <>
        Mirror a master account onto many accounts across brokers and cTrader IDs. Connections drop,
        orders bounce, tokens rotate — cMind reconciles without double-firing trades and keeps a full
        audit trail. <Link to="/docs/features/copy-trading">How it works →</Link>
      </>
    ),
  },
  {
    icon: <IconAi />,
    title: 'AI that does chores, not small talk',
    body: (
      <>
        Plain-English idea → a real, compiling cBot with a self-repair loop. Plus parameter tuning,
        backtest post-mortems, and a risk guard that can auto-stop a misbehaving bot.{' '}
        <Link to="/docs/features/ai">Meet the AI core →</Link>
      </>
    ),
  },
  {
    icon: <IconCode />,
    title: 'Build & backtest in the browser',
    body: (
      <>
        A Monaco IDE (yes, the VS Code one), C# and Python templates, and sandboxed{' '}
        <code>dotnet build</code> in throwaway containers. Watch equity curves stream back live.{' '}
        <Link to="/docs/features/build-and-backtest">Start building →</Link>
      </>
    ),
  },
  {
    icon: <IconNodes />,
    title: 'A fleet that scales & self-heals',
    body: (
      <>
        Runs and backtests spread across auto-discovering nodes. A node dies mid-job? Its lease is
        reclaimed and work carries on. No pager required.{' '}
        <Link to="/docs/deployment/scaling">Scale out →</Link>
      </>
    ),
  },
  {
    icon: <IconShield />,
    title: 'Prop-firm guardrails built in',
    body: (
      <>
        Model real challenge rules — daily loss, max drawdown, targets — with live equity tracking, so
        your bots stay inside the lines. <Link to="/docs/features/prop-firm">Pass the challenge →</Link>
      </>
    ),
  },
  {
    icon: <IconPalette />,
    title: 'White-label, top to bottom',
    body: (
      <>
        Swap the name, colors, logo, and favicon per tenant. Resellers ship cMind as their own product
        and nobody&apos;s the wiser. <Link to="/docs/features/white-label">Make it yours →</Link>
      </>
    ),
  },
  {
    icon: <IconPhone />,
    title: 'In your pocket (installable PWA)',
    body: (
      <>
        Mobile-first, fully responsive, installable on your phone with bottom-nav and an offline shell.
        Check on your bots from the bus. <Link to="/docs/features/pwa">Install it →</Link>
      </>
    ),
  },
  {
    icon: <IconPlug />,
    title: 'Talks to AI clients (MCP)',
    body: (
      <>
        A built-in MCP server (HTTP + SSE) exposes tools so Claude and friends can drive cMind for you.{' '}
        <Link to="/docs/features/mcp">Wire it up →</Link>
      </>
    ),
  },
  {
    icon: <IconShield />,
    title: 'Yours to run, hardened by default',
    body: (
      <>
        Argon2id, an encrypted key ring, per-node signed tokens, rate limiting, and OpenTelemetry out
        of the box. Your keys never leave your box. <Link to="/docs/deployment/local">Self-host →</Link>
      </>
    ),
  },
];

function Features() {
  return (
    <section className={`${styles.section} ${styles.sectionAlt}`}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}>Everything in the box</span>
        <Heading as="h2" className={styles.sectionTitle}>
          One app. The whole trading operation.
        </Heading>
        <p className={styles.sectionLede}>
          From &quot;I have an idea&quot; to &quot;it&apos;s live on twelve accounts&quot; — without
          gluing together six tools and a spreadsheet.
        </p>
      </div>
      <div className={styles.features}>
        {features.map((f) => (
          <div key={f.title} className={styles.feature}>
            <div className={styles.featureIcon}>{f.icon}</div>
            <h3 className={styles.featureTitle}>{f.title}</h3>
            <p className={styles.featureText}>{f.body}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

const showcases = [
  {
    img: '/img/screenshots/copy-trading-desktop.png',
    title: 'Copy trading, minus the panic',
    text: 'Per-destination control over sizing, direction, symbols, order types, SL/TP, expiry and exact market-range slippage. When something goes sideways, cMind resyncs instead of duplicating.',
    reverse: false,
  },
  {
    img: '/img/screenshots/ai-build-desktop.png',
    title: 'Describe it. Watch it compile.',
    text: 'Tell the AI what you want in plain English. It writes the cBot, builds it in a sandbox, reads the errors, and fixes itself — looping until it runs. You review the diff, not the stack trace.',
    reverse: true,
  },
  {
    img: '/img/screenshots/nodes-desktop.png',
    title: 'A cluster you barely have to think about',
    text: 'Agents self-register and heartbeat. The scheduler picks the least-loaded node. A dead node hands its work back automatically. It feels like infrastructure because it is.',
    reverse: false,
  },
  {
    img: '/img/screenshots/dashboard-mobile.png',
    title: 'Runs beautifully on a 360px phone',
    text: 'Same power, pocket-sized. Bottom-nav, card layouts, offline shell, add-to-home-screen. Every surface is designed mobile-first, then scales up — not the other way around.',
    reverse: true,
  },
];

function Showcase() {
  return (
    <section className={styles.section}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}>See it in action</span>
        <Heading as="h2" className={styles.sectionTitle}>
          Real screenshots. Real dark mode. Real product.
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

const deploys = [
  { icon: <IconDocker />, name: 'Local / Docker', hint: 'compose up & go', to: '/docs/deployment/local' },
  { icon: <IconK8s />, name: 'Kubernetes', hint: 'Helm chart', to: '/docs/deployment/kubernetes' },
  { icon: <IconCloud />, name: 'Azure', hint: 'step-by-step', to: '/docs/deployment/cloud-azure' },
  { icon: <IconCloud />, name: 'AWS', hint: 'step-by-step', to: '/docs/deployment/cloud-aws' },
];

function Deploy() {
  return (
    <section className={`${styles.section} ${styles.sectionAlt}`}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}>Deploy anywhere</span>
        <Heading as="h2" className={styles.sectionTitle}>
          Your servers, your rules.
        </Heading>
        <p className={styles.sectionLede}>
          Beginner-friendly, copy-paste guides for every option. Start on your laptop, graduate to a
          cluster when you&apos;re ready.
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

const audience = [
  {
    emoji: '📈',
    title: 'Algorithmic traders',
    text: 'You live in dark dashboards and equity curves. Write a cBot, backtest it across nodes, run it live — all in one place.',
  },
  {
    emoji: '🧑‍💻',
    title: 'Quant-leaning developers',
    text: 'C# or Python, a real IDE, sandboxed builds, an MCP server, and an API. Automate the boring parts with the AI.',
  },
  {
    emoji: '🏢',
    title: 'Prop firms & trading desks',
    text: 'Multi-tenant, white-label, compliance logs, and prop-firm rule simulation. Onboard traders under your own brand.',
  },
];

function Audience() {
  return (
    <section className={styles.section}>
      <div className={styles.sectionHead}>
        <span className={styles.eyebrow}>Who it&apos;s for</span>
        <Heading as="h2" className={styles.sectionTitle}>
          Built for people with money on the line.
        </Heading>
      </div>
      <div className={styles.audience}>
        {audience.map((a) => (
          <div key={a.title} className={styles.audCard}>
            <div className={styles.audEmoji}>{a.emoji}</div>
            <h3>{a.title}</h3>
            <p>{a.text}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

function FinalCta() {
  return (
    <section className={styles.cta}>
      <Heading as="h2" className={styles.ctaTitle}>
        Ready to meet your new trading desk?
      </Heading>
      <p className={styles.ctaText}>
        Read the friendly intro, then have it running locally before your coffee gets cold. It&apos;s
        open-source and MIT-licensed — poke around, break things, send a PR.
      </p>
      <div className={styles.heroButtons}>
        <Link className="button button--primary button--lg" to="/docs/intro">
          Read the docs →
        </Link>
        <Link
          className={`button button--lg ${styles.ghButton}`}
          href="https://github.com/amusleh-spotware-com/cmind"
        >
          Star on GitHub ★
        </Link>
      </div>
    </section>
  );
}

export default function Home(): ReactNode {
  return (
    <Layout
      title="cMind — your entire cTrader trading desk, in one app"
      description="Open-source, self-hostable, white-label trading operations platform for cTrader. Build, backtest, run and copy strategies at scale with AI built in."
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

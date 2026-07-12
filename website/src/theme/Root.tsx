import React, { useEffect, useState, type ReactNode } from 'react';
import { translate } from '@docusaurus/Translate';
import styles from './Root.module.css';

// A first-visit legal risk-disclaimer gate. Docusaurus renders <Root> around the whole app, so this
// overlay guards every page. Acceptance persists in localStorage (STORAGE_KEY) and never shows again;
// declining sends the visitor off the site. All strings go through Docusaurus translate() so the gate
// speaks the same 23 languages as the rest of the site (i18n/<locale>/code.json).

const STORAGE_KEY = 'cmind-risk-disclaimer-accepted';
const STORAGE_VERSION = '1';
const EXIT_URL = 'https://www.google.com';

export default function Root({ children }: { children: ReactNode }): ReactNode {
  // Undefined = not yet resolved on the client (avoids an SSR/hydration flash of the modal).
  const [accepted, setAccepted] = useState<boolean | undefined>(undefined);

  useEffect(() => {
    try {
      setAccepted(window.localStorage.getItem(STORAGE_KEY) === STORAGE_VERSION);
    } catch {
      // localStorage blocked (private mode / disabled) — show the gate, don't persist.
      setAccepted(false);
    }
  }, []);

  useEffect(() => {
    const locked = accepted === false;
    document.body.style.overflow = locked ? 'hidden' : '';
    return () => {
      document.body.style.overflow = '';
    };
  }, [accepted]);

  function onAccept(): void {
    try {
      window.localStorage.setItem(STORAGE_KEY, STORAGE_VERSION);
    } catch {
      // ignore persistence failure — still let the visitor through for this session.
    }
    setAccepted(true);
  }

  function onDecline(): void {
    window.location.replace(EXIT_URL);
  }

  return (
    <>
      {children}
      {accepted === false && (
        <div
          className={styles.overlay}
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="cmind-risk-title"
          aria-describedby="cmind-risk-body"
        >
          <div className={styles.dialog}>
            <h2 id="cmind-risk-title" className={styles.title}>
              {translate({
                id: 'riskDisclaimer.title',
                message: 'Risk Warning',
                description: 'Title of the first-visit risk-disclaimer dialog',
              })}
            </h2>
            <p id="cmind-risk-body" className={styles.body}>
              {translate({
                id: 'riskDisclaimer.body',
                message:
                  'Trading foreign exchange (Forex) and Contracts for Difference (CFDs) on margin carries a high level of risk and may not be suitable for all investors. The high degree of leverage can work against you as well as for you. You may sustain a loss of some or all of your invested capital, and therefore you should not invest money that you cannot afford to lose. Past performance and backtest results are not indicative of future results. cMind is self-hostable software provided "as is" and does not provide investment, financial, legal, or tax advice. Any and all trading decisions, and any resulting profits or losses, are made at your own discretion and are your sole and full responsibility.',
                description: 'Body text of the first-visit risk-disclaimer dialog',
              })}
            </p>
            <p className={styles.prompt}>
              {translate({
                id: 'riskDisclaimer.prompt',
                message:
                  'To continue you must confirm that you understand and accept these risks. If you do not accept, you cannot use this site.',
                description: 'Instruction line above the accept / decline buttons',
              })}
            </p>
            <div className={styles.actions}>
              <button type="button" className={styles.decline} onClick={onDecline}>
                {translate({
                  id: 'riskDisclaimer.decline',
                  message: 'I do not accept — leave',
                  description: 'Decline button that navigates the visitor off the site',
                })}
              </button>
              <button type="button" className={styles.accept} onClick={onAccept}>
                {translate({
                  id: 'riskDisclaimer.accept',
                  message: 'I understand and accept',
                  description: 'Accept button that dismisses the risk-disclaimer gate',
                })}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

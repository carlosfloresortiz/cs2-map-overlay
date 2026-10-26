import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { FloatingButton, Panel } from "cs2/ui";
import * as b from "./bindings";
import icon from "images/map-overlay.svg";
import { OpacitySlider } from "./opacity-slider";
import styles from "./panel.module.scss";

const usePanelOpen = () => useSyncExternalStore(b.panelStore.subscribe, b.panelStore.get);

const useText = () => {
  const { translate } = useLocalization();
  return (key: string, fallback: string) => translate("MapOverlay.UI." + key, fallback) ?? fallback;
};

const cx = (...names: (string | false | undefined)[]) => names.filter(Boolean).join(" ");

// Appended to "GameTopLeft": the game lays these out in a row next to other mods' buttons.
export const ToolbarButton = () => {
  const open = usePanelOpen();
  const text = useText();
  return (
    <FloatingButton
      src={icon}
      selected={open}
      tooltipLabel={text("Title", "Map Overlay")}
      onSelect={() => b.panelStore.set(!open)}
    />
  );
};

const Toggle = ({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) => (
  <button className={cx(styles.toggle, value && styles.toggleOn)} onClick={() => onChange(!value)}>
    <span className={styles.checkbox}>{value ? "✓" : ""}</span>
    <span>{label}</span>
  </button>
);

const Stepper = ({ label, value, onChange }: { label: string; value: string; onChange: (delta: number) => void }) => (
  <div className={styles.stepper}>
    <span className={styles.label}>{label}</span>
    <div className={styles.stepperControls}>
      <button className={styles.stepButton} onClick={() => onChange(-1)}>
        −
      </button>
      <span className={styles.stepperValue}>{value}</span>
      <button className={styles.stepButton} onClick={() => onChange(1)}>
        +
      </button>
    </div>
  </div>
);

const SearchBox = () => {
  const text = useText();
  const status = useValue(b.searchStatus$);
  const resultsJson = useValue(b.searchResults$);
  const [query, setQuery] = useState("");
  const timer = useRef<number | undefined>(undefined);
  let results: b.SearchResult[] = [];
  try {
    results = JSON.parse(resultsJson);
  } catch {
    results = [];
  }

  useEffect(() => () => window.clearTimeout(timer.current), []);

  // Suggestions follow typing after a short pause, so each keystroke is not a request.
  const type = (value: string) => {
    setQuery(value);
    window.clearTimeout(timer.current);
    const trimmed = value.trim();
    if (trimmed.length < 3) {
      b.clearSearch();
      return;
    }
    timer.current = window.setTimeout(() => b.suggest(trimmed), 350);
  };

  const submit = () => {
    window.clearTimeout(timer.current);
    if (query.trim().length > 0) b.search(query);
  };

  return (
    <div className={styles.section}>
      <div className={styles.searchRow}>
        <input
          className={styles.searchInput}
          type="text"
          value={query}
          placeholder={text("SearchPlaceholder", "Place, address or coordinates")}
          onChange={(e) => type(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") submit();
          }}
        />
        <button className={styles.button} onClick={submit}>
          {text("Search", "Search")}
        </button>
      </div>
      {status === "searching" && <div className={styles.hint}>{text("Searching", "Searching…")}</div>}
      {status === "empty" && <div className={styles.hint}>{text("NoResults", "No results.")}</div>}
      {status === "error" && <div className={styles.warning}>{text("SearchError", "Search failed.")}</div>}
      {results.length > 0 && (
        <div className={styles.results}>
          {results.map((r, i) => (
            <button
              key={i}
              className={styles.result}
              onClick={() => {
                window.clearTimeout(timer.current);
                setQuery(r.name);
                b.goTo(r.lat, r.lon);
                b.clearSearch();
              }}
            >
              {r.name}
            </button>
          ))}
          <div className={styles.hint}>{text("SearchHint", "The chosen place appears where the camera looks.")}</div>
        </div>
      )}
    </div>
  );
};

const steps = [1, 5, 25, 100];

const MovePad = () => {
  const text = useText();
  const [step, setStep] = useState(5);
  const rotation = useValue(b.rotation$);
  const rotate = (delta: number) => b.setRotation(Math.round((rotation + delta) * 10) / 10);

  return (
    <div className={styles.section}>
      <div className={styles.sectionTitle}>{text("Move", "Move map")}</div>
      <div className={styles.moveRow}>
        <div className={styles.pad}>
          <div />
          <button className={styles.padButton} onClick={() => b.nudge(0, step)}>
            ▲
          </button>
          <div />
          <button className={styles.padButton} onClick={() => b.nudge(-step, 0)}>
            ◀
          </button>
          <button
            className={styles.padButton}
            title={text("BringHere", "Bring centre here")}
            onClick={() => b.bringHere()}
          >
            ◎
          </button>
          <button className={styles.padButton} onClick={() => b.nudge(step, 0)}>
            ▶
          </button>
          <div />
          <button className={styles.padButton} onClick={() => b.nudge(0, -step)}>
            ▼
          </button>
          <div />
        </div>
        <div className={styles.moveSide}>
          <span className={styles.label}>{text("Step", "Step")}</span>
          <div className={styles.chips}>
            {steps.map((s) => (
              <button key={s} className={cx(styles.chip, s === step && styles.chipOn)} onClick={() => setStep(s)}>
                {s} m
              </button>
            ))}
          </div>
          <button className={styles.linkButton} onClick={() => b.bringHere()}>
            ◎ {text("BringHere", "Bring centre here")}
          </button>
        </div>
      </div>
      <div className={styles.hint}>{text("MoveHint", "Arrows follow the camera view.")}</div>

      <div className={styles.rowHeader}>
        <span className={styles.label}>{text("Rotation", "Rotation")}</span>
        <span className={styles.value}>{rotation.toFixed(1)}°</span>
      </div>
      <div className={styles.rotateRow}>
        <button className={styles.button} onClick={() => rotate(-5)}>
          −5°
        </button>
        <button className={styles.button} onClick={() => rotate(-0.5)}>
          −0.5°
        </button>
        <button className={styles.button} onClick={() => b.setRotation(0)}>
          {text("Reset", "Reset")}
        </button>
        <button className={styles.button} onClick={() => rotate(0.5)}>
          +0.5°
        </button>
        <button className={styles.button} onClick={() => rotate(5)}>
          +5°
        </button>
      </div>
    </div>
  );
};

const MapSettings = () => {
  const text = useText();
  const provider = useValue(b.provider$);
  const zoom = useValue(b.zoom$);
  const area = useValue(b.area$);
  const onTop = useValue(b.onTop$);
  const follow = useValue(b.follow$);
  const count = b.providers.length;
  const p = b.providers[provider] ?? b.providers[0];

  return (
    <div className={styles.section}>
      <Stepper
        label={text("Style", "Style")}
        value={text("Provider." + p.key, p.fallback)}
        onChange={(d) => b.setProvider((provider + d + count) % count)}
      />
      <Stepper label={text("Detail", "Detail")} value={`${zoom}`} onChange={(d) => b.setZoom(zoom + d)} />
      <Stepper label={text("Area", "Area")} value={`${area} km`} onChange={(d) => b.setArea(area + d)} />
      <Toggle label={text("OnTop", "On top of everything")} value={onTop} onChange={b.setOnTop} />
      <Toggle label={text("Follow", "Load where you look")} value={follow} onChange={b.setFollow} />
    </div>
  );
};

const Status = () => {
  const text = useText();
  const ready = useValue(b.tilesReady$);
  const total = useValue(b.tilesTotal$);
  const failed = useValue(b.tilesFailed$);
  const problem = useValue(b.problem$);
  const lat = useValue(b.lat$);
  const lon = useValue(b.lon$);
  const attribution = useValue(b.attribution$);
  const problems: Record<string, [string, string]> = {
    Network: ["ProblemNetwork", "Some tiles did not download."],
    Blocked: ["ProblemBlocked", "The server asked to wait."],
    BadUrl: ["ProblemBadUrl", "The custom URL is not valid."],
  };
  const message = problems[problem];

  return (
    <div className={styles.footer}>
      {message && <div className={styles.warning}>{text(message[0], message[1])}</div>}
      <div className={styles.statusLine}>
        <span>
          {text("Tiles", "Tiles")} {ready}/{total}
          {failed > 0 ? ` · ${failed} ${text("Failed", "failed")}` : ""}
        </span>
        <span>
          {lat.toFixed(5)}, {lon.toFixed(5)}
        </span>
      </div>
      {attribution && <div className={styles.attribution}>{attribution}</div>}
    </div>
  );
};

// Appended to "Game": rendered over the game view while open.
export const OverlayPanel = () => {
  const open = usePanelOpen();
  const visible = useValue(b.visible$);
  const opacity = useValue(b.opacity$);
  const text = useText();
  if (!open) return null;

  return (
    <Panel
      className={styles.panel}
      header={<div className={styles.title}>{text("Title", "Map Overlay")}</div>}
      onClose={() => b.panelStore.set(false)}
    >
      <div className={styles.scroll}>
        <Toggle label={text("Visible", "Show map")} value={visible} onChange={b.setVisible} />
        <SearchBox />
        <OpacitySlider label={text("Opacity", "Opacity")} value={opacity} onCommit={b.setOpacity} />
        <div className={styles.separator} />
        <MovePad />
        <div className={styles.separator} />
        <MapSettings />
        <Status />
      </div>
    </Panel>
  );
};

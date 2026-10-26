import { useEffect, useRef, useState } from "react";
import styles from "./panel.module.scss";

interface OpacitySliderProps {
  label: string;
  value: number;
  disabled?: boolean;
  onCommit: (value: number) => void;
}

// cs2/ui exposes no slider, so this is a small pointer-driven one. The value is shown
// live while dragging and sent to the game once, on release.
export const OpacitySlider = ({ label, value, disabled, onCommit }: OpacitySliderProps) => {
  const trackRef = useRef<HTMLDivElement>(null);
  const [dragValue, setDragValue] = useState<number | null>(null);
  const shown = dragValue ?? value;

  const valueAt = (clientX: number) => {
    const rect = trackRef.current!.getBoundingClientRect();
    const ratio = rect.width > 0 ? (clientX - rect.left) / rect.width : 0;
    return Math.round(Math.max(0, Math.min(1, ratio)) * 100);
  };

  useEffect(() => {
    if (dragValue === null) return;
    const move = (e: MouseEvent) => setDragValue(valueAt(e.clientX));
    const up = (e: MouseEvent) => {
      const final = valueAt(e.clientX);
      setDragValue(null);
      onCommit(final);
    };
    document.addEventListener("mousemove", move);
    document.addEventListener("mouseup", up);
    return () => {
      document.removeEventListener("mousemove", move);
      document.removeEventListener("mouseup", up);
    };
  }, [dragValue !== null]);

  const step = (delta: number) => onCommit(Math.max(0, Math.min(100, value + delta)));

  return (
    <div className={disabled ? `${styles.row} ${styles.disabled}` : styles.row}>
      <div className={styles.rowHeader}>
        <span className={styles.label}>{label}</span>
        <span className={styles.value}>{shown}%</span>
      </div>
      <div className={styles.sliderLine}>
        <button className={styles.stepButton} onClick={() => step(-5)}>
          −
        </button>
        <div
          ref={trackRef}
          className={styles.track}
          onMouseDown={(e) => {
            if (e.button !== 0) return;
            setDragValue(valueAt(e.clientX));
          }}
        >
          <div className={styles.fill} style={{ width: `${shown}%` }} />
          <div className={styles.thumb} style={{ left: `${shown}%` }} />
        </div>
        <button className={styles.stepButton} onClick={() => step(5)}>
          +
        </button>
      </div>
    </div>
  );
};

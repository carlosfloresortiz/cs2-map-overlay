import { bindValue, trigger } from "cs2/api";

// Must match MapOverlayUISystem.cs.
const group = "mapOverlay";

export const visible$ = bindValue<boolean>(group, "visible", false);
export const opacity$ = bindValue<number>(group, "opacity", 50);
export const onTop$ = bindValue<boolean>(group, "onTop", true);
export const follow$ = bindValue<boolean>(group, "follow", true);
export const zoom$ = bindValue<number>(group, "zoom", 17);
export const area$ = bindValue<number>(group, "area", 3);
export const rotation$ = bindValue<number>(group, "rotation", 0);
export const lat$ = bindValue<number>(group, "lat", 0);
export const lon$ = bindValue<number>(group, "lon", 0);
export const provider$ = bindValue<number>(group, "provider", 0);
export const attribution$ = bindValue<string>(group, "attribution", "");
export const tilesReady$ = bindValue<number>(group, "tilesReady", 0);
export const tilesTotal$ = bindValue<number>(group, "tilesTotal", 0);
export const tilesFailed$ = bindValue<number>(group, "tilesFailed", 0);
export const problem$ = bindValue<string>(group, "problem", "");
export const searchStatus$ = bindValue<string>(group, "searchStatus", "");
export const searchResults$ = bindValue<string>(group, "searchResults", "[]");

export const setVisible = (v: boolean) => trigger(group, "setVisible", v);
export const setOpacity = (v: number) => trigger(group, "setOpacity", v);
export const setOnTop = (v: boolean) => trigger(group, "setOnTop", v);
export const setFollow = (v: boolean) => trigger(group, "setFollow", v);
export const setZoom = (v: number) => trigger(group, "setZoom", v);
export const setArea = (v: number) => trigger(group, "setArea", v);
export const setProvider = (v: number) => trigger(group, "setProvider", v);
export const setRotation = (v: number) => trigger(group, "setRotation", v);
export const nudge = (right: number, forward: number) => trigger(group, "nudge", right, forward);
export const bringHere = () => trigger(group, "bringHere");
export const search = (query: string) => trigger(group, "search", query);
export const suggest = (query: string) => trigger(group, "suggest", query);
export const clearSearch = () => trigger(group, "clearSearch");
export const goTo = (lat: number, lon: number) => trigger(group, "goTo", lat, lon);

// Same order as the TileProvider enum in Setting.cs.
export const providers = [
  { key: "OpenStreetMap", fallback: "OpenStreetMap" },
  { key: "CartoLight", fallback: "CARTO light" },
  { key: "CartoLightNoLabels", fallback: "CARTO light, no labels" },
  { key: "CartoVoyager", fallback: "CARTO Voyager" },
  { key: "OpenTopoMap", fallback: "OpenTopoMap" },
  { key: "Custom", fallback: "Custom" },
];

export interface SearchResult {
  name: string;
  lat: number;
  lon: number;
}

// Panel visibility is UI-only state shared by the toolbar button and the panel.
type Listener = () => void;
let panelOpen = false;
const listeners = new Set<Listener>();

export const panelStore = {
  get: () => panelOpen,
  set: (open: boolean) => {
    panelOpen = open;
    listeners.forEach((l) => l());
  },
  subscribe: (listener: Listener) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};

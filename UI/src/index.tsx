import { ModRegistrar } from "cs2/modding";
import { OverlayPanel, ToolbarButton } from "mods/panel";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", ToolbarButton);
    moduleRegistry.append("Game", OverlayPanel);
}

export default register;

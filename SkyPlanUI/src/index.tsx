import { ModuleRegistry } from 'cs2/modding';
import SkyplanOverlay from './mods/SkyplanOverlay';

// Appended to the 'Game' hook — renders inside the main game screen only
export default (moduleRegistry: ModuleRegistry) => {

  moduleRegistry.append('Game', SkyplanOverlay);
};

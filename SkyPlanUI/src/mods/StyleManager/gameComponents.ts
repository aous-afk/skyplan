import {getModule} from 'cs2/modding';

export const Slider = getModule('game-ui/common/input/slider/slider.tsx', 'Slider') as any;
export const CheckBox = getModule('game-ui/common/input/toggle/checkbox/checkbox.tsx', 'Checkbox') as any;
// Swatch button that opens the game's ColorPicker in a popup; value / onChange are {r,g,b,a} 0-1.
export const ColorField = getModule('game-ui/common/input/color-picker/color-field/color-field.tsx', 'ColorField') as any;

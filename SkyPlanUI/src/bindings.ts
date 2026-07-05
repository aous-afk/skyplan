import { bindValue } from 'cs2/api';

export const panelVisible$ = bindValue<boolean>('skyplan', 'panelVisible', false);
export const shapes$ = bindValue<string> ('skyplan', 'shapes', '[]');
export const shapesBaseline$ = bindValue<string> ('skyplan', 'shapesBaseline', '[]');
export const preview$ = bindValue<string> ('skyplan', 'preview', '');
export const highlight$ = bindValue<string> ('skyplan', 'highlight', '');
export const transform$ = bindValue<string> ('skyplan', 'transform', '');
export const layersConfig$ = bindValue<string>('skyplan', 'layersConfig', '{"layers":[]}');

import { bindValue } from 'cs2/api';

export const panelVisible$ = bindValue<boolean>('skyplan', 'panelVisible', false);
export const shapes$        = bindValue<string> ('skyplan', 'shapes',       '[]');
export const preview$       = bindValue<string> ('skyplan', 'preview',      '');
export const highlight$     = bindValue<string> ('skyplan', 'highlight',    '');

declare module "cs2/l10n" {
  export const useLocalization: () => { translate: (id: string) => string };
}

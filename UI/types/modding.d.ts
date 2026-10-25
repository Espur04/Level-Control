declare module "cs2/modding" {
  export function getModule(modulePath: string, exportName: string): unknown;
  export type ModRegistrar = (moduleRegistry: {
    extend: (modulePath: string, exportName: string, extender: (componentList: Record<string, unknown>) => Record<string, unknown>) => void;
  }) => void;
}

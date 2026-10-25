declare module "cs2/api" {
  export const trigger: (modId: string, eventName: string, ...args: any[]) => void;
  export const bindLocalValue: <T>(value: T) => { value: T; update: (value: T) => void };
  export const useValue: <T>(binding: { value: T }) => T;
}

declare module "*.svg" {
  const url: string;
  export default url;
}

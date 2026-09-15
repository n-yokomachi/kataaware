export function requiredIds(items: readonly { id: string; required?: boolean }[]): string[] {
  return items.filter((i) => i.required).map((i) => i.id);
}

export function isComplete(required: readonly string[], done: ReadonlySet<string>): boolean {
  return required.every((id) => done.has(id));
}

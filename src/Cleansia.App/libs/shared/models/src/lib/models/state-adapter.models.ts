export class StateAdapter<T> {
  private state: T;

  constructor(initialState: T) {
    this.state = initialState;
  }

  updateState(partialState: Partial<T>): T {
    this.state = { ...this.state, ...partialState };
    return this.state;
  }
}

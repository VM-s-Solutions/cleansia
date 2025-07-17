export class StateAdapter<T> {
  private state: T;

  constructor(initialState: T) {
    this.state = initialState;
  }

  static create<T>(initialState: T): StateAdapter<T> {
    return new StateAdapter(initialState);
  }

  getState(): T {
    return this.state;
  }

  setState(newState: T): T {
    this.state = newState;
    return this.state;
  }

  updateState(partialState: Partial<T>): T {
    this.state = { ...this.state, ...partialState };
    return this.state;
  }

  resetState(initialState: T): T {
    this.state = initialState;
    return this.state;
  }
}

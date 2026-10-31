// $app/state and the view the router renders. Reassigning a field is what components react to,
// so each holds its value raw (no deep proxy around URL, data or components).
class PageState {
  url = $state.raw(typeof location === 'undefined' ? null : new URL(location.href));
  params = $state.raw({});
  route = $state.raw({ id: null });
  status = $state.raw(200);
  error = $state.raw(null);
  data = $state.raw({});
  form = $state.raw(null);
  state = $state.raw({});
}

class View {
  chain = $state.raw([]);
}

export const page = new PageState();
export const view = new View();

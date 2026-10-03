import { PackageListItem } from '../client/customer-client';
import { chosenPackagesByService, includedServicesAlreadyChosen } from './package-overlap';

function pkg(id: string, serviceIds: string[]): PackageListItem {
  return PackageListItem.fromJS({
    id,
    name: id,
    price: 100,
    includedServices: serviceIds.map((serviceId) => ({ serviceId, name: serviceId })),
  });
}

describe('chosenPackagesByService', () => {
  const deep = pkg('deep', ['windows', 'oven']);
  const kitchen = pkg('kitchen', ['oven', 'fridge']);

  it('lists the services of the chosen packages only', () => {
    const byService = chosenPackagesByService([deep, kitchen], ['deep']);

    expect([...byService.keys()]).toEqual(['windows', 'oven']);
    expect(byService.get('windows')).toEqual([deep]);
    expect(byService.has('fridge')).toBe(false);
  });

  it('names every chosen package that includes a service', () => {
    const byService = chosenPackagesByService([deep, kitchen], ['deep', 'kitchen']);

    expect(byService.get('oven')).toEqual([deep, kitchen]);
  });

  it('is empty while no package is chosen', () => {
    expect(chosenPackagesByService([deep, kitchen], []).size).toBe(0);
  });

  it('names a package once even when it lists a service twice', () => {
    const twice = pkg('twice', ['windows', 'windows']);

    expect(chosenPackagesByService([twice], ['twice']).get('windows')).toEqual([twice]);
  });
});

describe('includedServicesAlreadyChosen', () => {
  const deep = pkg('deep', ['windows', 'oven', 'floors']);

  it('returns the included services that are chosen on their own, in the package order', () => {
    const overlap = includedServicesAlreadyChosen(deep, ['floors', 'windows', 'ironing']);

    expect(overlap.map((s) => s.serviceId)).toEqual(['windows', 'floors']);
  });

  it('is empty when nothing the package includes is chosen, or the package is unknown', () => {
    expect(includedServicesAlreadyChosen(deep, ['ironing'])).toEqual([]);
    expect(includedServicesAlreadyChosen(undefined, ['windows'])).toEqual([]);
  });
});

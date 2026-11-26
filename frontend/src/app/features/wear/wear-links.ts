// A wear page: its translation key (wear.links.{key} on the Oversigt page, wear.menu.{key} in the Wear menu), its
// address, and the privileges that may open it
export interface WearLink {
  key: string;
  path: string;
  privileges: string[];
}

// Any wear privilege opens the Wear menu and its Oversigt page
export const WEAR_PRIVILEGES = ['wear_view', 'wear_edit', 'wear_list_view', 'wear_distribute_edit'];

// The pages of Infosys's Wear menu after "Oversigt", in its order. Infosys's Oversigt page (wear/, main.phtml) lists
// six of them after "Du kan:"; "Rediger wear egenskaber" and "Upload billede til wear" are listed there too. Infosys's
// Oversigt link to the orders not handed out went to all orders; it goes to the right list here. Each link is shown to
// the users who may open the page.
export const WEAR_LINKS: WearLink[] = [
  { key: 'breakdown', path: '/wear/breakdown', privileges: ['wear_view', 'wear_edit'] },
  { key: 'orders', path: '/wear/orders', privileges: ['wear_list_view', 'wear_distribute_edit'] },
  { key: 'unfilled', path: '/wear/orders/unfilled', privileges: ['wear_list_view', 'wear_distribute_edit'] },
  { key: 'types', path: '/wear/types', privileges: ['wear_view', 'wear_edit'] },
  { key: 'create', path: '/wear/types/new', privileges: ['wear_edit'] },
  { key: 'attributes', path: '/wear/attributes', privileges: ['wear_edit'] },
  { key: 'uploadImage', path: '/wear/images/upload', privileges: ['wear_edit'] },
  { key: 'handout', path: '/wear/handout', privileges: ['wear_distribute_edit'] },
];

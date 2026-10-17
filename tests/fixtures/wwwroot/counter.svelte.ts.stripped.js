import { format } from './format.ts';

                   
                         
                               
 

export function counter(start        )          {
  let count         = $state(start);
  return {
    get label()         {
      return format(count);
    },
    increment(by         = 1)       {
      count += by;
    },
  };
}

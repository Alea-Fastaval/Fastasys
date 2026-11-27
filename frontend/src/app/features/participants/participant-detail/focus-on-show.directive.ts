import { AfterViewInit, Directive, ElementRef, inject } from '@angular/core';

// Puts the cursor in a text field as soon as it is shown, after the text it already has
@Directive({
  selector: 'textarea[appFocusOnShow]',
})
export class FocusOnShowDirective implements AfterViewInit {
  private readonly element = inject<ElementRef<HTMLTextAreaElement>>(ElementRef);

  public ngAfterViewInit(): void {
    const textarea = this.element.nativeElement;
    textarea.focus();
    textarea.setSelectionRange(textarea.value.length, textarea.value.length);
  }
}

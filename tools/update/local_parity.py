"""Compatibility entry point for the translation update tool."""
if __package__:
    from .translation_update import GUARDED, apply_review, main, make_plan, review_template, validate_translation
else:
    from translation_update import main

if __name__ == '__main__':
    main()

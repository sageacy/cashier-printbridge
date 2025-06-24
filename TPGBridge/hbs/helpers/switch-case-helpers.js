module.exports = {
  /**
   * @param {any} value The value to switch on
   * @param {object} options Handlebars options object
   */
  switch: function(value, options) {
    // Attach the value to switch on and a flag to the current context.
    // This context will be available to the nested #case and #default helpers.
    this.switch_value = value;
    this.switch_case_executed = false; // To ensure only one case (or default) executes

    // Render the inner content (the #case and #default blocks)
    const html = options.fn(this);

    // Clean up context variables after execution (optional, but good practice)
    delete this.switch_value;
    delete this.switch_case_executed;

    return html;
  },

  /**
   * @param {any} value The value for this case
   * @param {object} options Handlebars options object
   */
  case: function(value, options) {
    // Check if this case matches the switch_value and no other case has been executed
    if (value === this.switch_value && !this.switch_case_executed) {
      this.switch_case_executed = true;
      return options.fn(this); // Render the content of this case
    }
    return ''; // Return empty if case doesn't match or another case already executed
  },

  /**
   * @param {object} options Handlebars options object
   */
  default: function(options) {
    // Execute only if no #case block has matched and executed
    if (!this.switch_case_executed) {
      // No need to set switch_case_executed = true here, as it's the fallback
      return options.fn(this); // Render the content of the default block
    }
    return ''; // Return empty if a case already executed
  }
};